using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Game scene: the grocery store's copy (seen through its glass) goes dead once Khalid has come out of the back door - its
/// tube lights stutter out, the fridges stop humming and their lit fronts go dark - telling the player there is nothing
/// more in there. Call PowerOff() (the after-store cutscene does, see the component on Cutscene_AfterStore); the save
/// remembers it, so a later load finds the store dark straight away.
/// </summary>
public class StoreInteriorPowerOff : MonoBehaviour
{
    public const string SaveKey = "game:store-power-off";
    public static StoreInteriorPowerOff Instance { get; private set; }

    [Tooltip("Everything of the store copy (the 'Store Interior (from Store scene)' root and the store makeover's Game copy).")]
    [SerializeField] Transform[] roots;
    [Tooltip("The daylight ambient still lights the room through the glass: without its lamps the inside is darkened to this (albedo multiplier).")]
    [Range(0f, 1f)] public float darkness = 0.28f;
    [Tooltip("Objects whose name contains one of these stay as they are (they are outside, in daylight).")]
    public string[] keepLit = { "Outside" };

    bool off;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (SaveManager.instance != null && SaveManager.instance.IsInteractableUsed(SaveKey)) PowerOff(true);
    }

    public void PowerOff() => PowerOff(false);

    bool OutsideTheStore(Transform t)
    {
        for (; t != null; t = t.parent)
            foreach (var k in keepLit) if (!string.IsNullOrEmpty(k) && t.name.Contains(k)) return true;
        return false;
    }

    public void PowerOff(bool instant)
    {
        if (off) return;
        off = true;
        if (SaveManager.instance != null) SaveManager.instance.RegisterUsedInteractable(SaveKey);
        var seen = new HashSet<Object>();
        foreach (var root in roots)
        {
            if (root == null) continue;
            foreach (var f in root.GetComponentsInChildren<FluorescentFixture>(true))
            {
                if (!seen.Add(f)) continue;
                f.restoredMode = FluorescentFixture.Mode.Off;
                if (f.isActiveAndEnabled) f.Cut(); else f.mode = FluorescentFixture.Mode.Off;
            }
            foreach (var h in root.GetComponentsInChildren<FridgeHum>(true))
            {
                if (!seen.Add(h)) continue;
                var src = h.GetComponent<AudioSource>();
                if (instant || !h.isActiveAndEnabled) { h.enabled = false; if (src != null) src.Stop(); }
                else h.SetPowered(false);
                h.enabled = false;           // OnEnable would start it again
            }
            // anything else that lights itself: fridge fronts, signs inside, downlights
            foreach (var l in root.GetComponentsInChildren<Light>(true))
                if (seen.Add(l) && l.GetComponentInParent<FluorescentFixture>() == null) l.enabled = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || OutsideTheStore(r.transform)) continue;
                // no lamps, only the dull light through the glass
                foreach (var m in r.materials)
                    if (m != null && m.HasProperty("_Color") && m.renderQueue < 3000) m.color = m.color * new Color(darkness, darkness, darkness, 1f);
                if (r.GetComponentInParent<FluorescentFixture>() != null) continue;
                var mats = r.sharedMaterials;
                bool glows = false;
                foreach (var m in mats) if (m != null && m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.01f) glows = true;
                if (!glows) continue;
                var copies = r.materials;
                foreach (var m in copies) if (m != null && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            }
        }
        Debug.Log("[StoreInteriorPowerOff] the store copy is dark" + (instant ? " (from the save)" : ""));
    }
}
