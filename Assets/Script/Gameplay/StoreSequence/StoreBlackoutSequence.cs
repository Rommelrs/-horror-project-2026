using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

/// <summary>
/// Phase 3 of the store level - blackout and storage crash.
///  1. A few seconds after the entrance door locks the power dies: the fridge hums wind down and stop, the lights stutter out.
///  2. Total silence for a good while, so the player takes in how much the store has changed.
///  3. A heavy metallic crash from the storage room - the tall shelf there has fallen (StorageCollapse).
/// The storage door stays locked; nothing here touches it.
/// </summary>
[DefaultExecutionOrder(1000)]
public class StoreBlackoutSequence : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("Starts when the whole Runner sequence is over (door locked, he ran off, the far running footsteps have faded) so the silence after the power cut is really silent.")]
    [SerializeField] StoreFootstepsSequence footsteps;
    [Tooltip("Seconds after the Runner sequence ends before the power fails.")]
    [SerializeField] float blackoutDelay = 5f;

    [Header("Fuse box")]
    [Tooltip("The fuse must never go in before the power is cut. The moment the player stands at the fuse box with the fuse (when 'Press I to open inventory' would appear) the Runner scene is skipped, the door locks at once and the power is cut.")]
    [SerializeField] bool blackoutWhenPlayerReachesFusebox = true;
    [Tooltip("Delay before the power cut when it is triggered by reaching the fuse box.")]
    [SerializeField] float expediteDelay = 0.2f;
    [Tooltip("Safety net: if the player has had the fuse this long and the blackout has not even started, force it.")]
    [SerializeField] float failsafeSecondsAfterFuse = 90f;
    [Tooltip("The fuse can be installed this long after the crash (never before the power has been cut).")]
    [SerializeField] float fuseUsableAfterCrash = 2f;

    [Header("Power cut")]
    [Tooltip("Fridge / vending-machine hums. Leave empty to use every FridgeHum in the scene.")]
    [SerializeField] FridgeHum[] humEmitters;
    [Tooltip("Lights that go out (the Enviro directional light that lights the store, the storage-room spots ...).")]
    [SerializeField] Light[] lights;
    [Tooltip("Light tubes etc. whose emission goes black.")]
    [SerializeField] Renderer[] emissiveRenderers;
    [Tooltip("Looping ambience that is faded out for the silence (not the fridges - those have their own wind-down).")]
    [SerializeField] AudioSource[] ambienceToFade;
    [SerializeField] float ambienceFadeSeconds = 1.2f;
    [Tooltip("The lights stutter this way before dying: pairs of (seconds off, seconds on).")]
    [SerializeField] Vector2[] stutter = { new Vector2(0.10f, 0.12f), new Vector2(0.06f, 0.20f), new Vector2(0.25f, 0.05f) };

    [Header("Ambient light")]
    [Tooltip("The store has no real lamps besides the sun light, so the sky/ambient fill would keep the room bright. While the power is out the ambient light is multiplied by this.")]
    [SerializeField, Range(0f, 1f)] float blackoutAmbientScale = 0.1f;
    [SerializeField] float ambientFadeSeconds = 0.8f;

    [Header("Fluorescent fixtures")]
    [Tooltip("Every ceiling fixture. They stutter out with the power cut and, when the fuse is in, each one does what its own 'Restored Mode' says (some On, some stay Off, a few Flicker).")]
    [SerializeField] FluorescentFixture[] fixtures;

    [Header("Fuse is back (partial power)")]
    [Tooltip("The fuse box. When the fuse goes in, the lights come back dimly (not the sun light - it stays off).")]
    [SerializeField] Fusebox fusebox;
    [Tooltip("Extra lights that only come on when the power is restored (the store ceiling lights). They stay off until then.")]
    [SerializeField] Light[] restoredLights;
    [Tooltip("How bright the restored lights are, as a fraction of their normal intensity.")]
    [SerializeField, Range(0f, 1f)] float restoredLightLevel = 0.45f;
    [Tooltip("Ambient light once the power is back (1 = as before the blackout).")]
    [SerializeField, Range(0f, 1f)] float restoredAmbientScale = 0.4f;
    [Tooltip("Flicker when the lights come back: pairs of (seconds off, seconds on).")]
    [SerializeField] Vector2[] restoreStutter = { new Vector2(0.15f, 0.10f), new Vector2(0.08f, 0.25f), new Vector2(0.20f, 0.12f) };

    [Header("Silence")]
    [Tooltip("How long it is quiet between the power cut and the crash.")]
    [SerializeField] float silenceSeconds = 8f;

    [Header("Audio")]
    [SerializeField] AudioClip powerCutClip;
    [Tooltip("The swell + thunk when the fuse goes in and the lights come back.")]
    [SerializeField] AudioClip powerOnClip;
    [SerializeField, Range(0f, 1f)] float powerOnVolume = 0.85f;
    [SerializeField] AudioClip crashClip;
    [SerializeField] AudioMixerGroup sfxGroup;
    [Tooltip("Where the power cut is heard from (the fuse box).")]
    [SerializeField] Transform powerCutPoint;
    [Tooltip("Where the crash comes from (inside the storage room).")]
    [SerializeField] Transform crashPoint;
    [SerializeField, Range(0f, 1f)] float powerCutVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] float crashVolume = 1f;
    [Tooltip("Extra non-positional layer so the sound stays clear wherever the player stands: volume at point-blank, fading to 0 at 'hearRange'.")]
    [SerializeField, Range(0f, 1f)] float flatLayerVolume = 0.3f;
    [SerializeField] float hearRange = 60f;
    [SerializeField] float crashMaxDistance = 70f;

    [Header("Collapse")]
    [SerializeField] StorageCollapse collapse;
    [Tooltip("Delay between the first sound of the crash and the shelf moving (the sound starts with the creak/groan).")]
    [SerializeField] float collapseLead = 0.0f;

    [Header("Events")]
    public UnityEvent onBlackout;
    public UnityEvent onCrash;
    public UnityEvent onFinished;

    public bool Started { get; private set; }
    public bool PowerIsOut { get; private set; }
    public bool Finished { get; private set; }

    readonly List<AudioSource> pool = new List<AudioSource>();
    Camera cam;
    float ambientTarget = 1f;
    float ambientK = 1f;
    readonly Dictionary<Light, float> normalIntensity = new Dictionary<Light, float>();
    readonly Dictionary<Renderer, Color> normalEmission = new Dictionary<Renderer, Color>();
    // ambient values as the sky system last set them, and the scaled values this script last wrote
    Color baseLight, baseSky, baseEquator, baseGround;
    Color wroteLight, wroteSky, wroteEquator, wroteGround;
    bool haveWrite;
    float delayLeft;
    bool expedited;
    float fuseHeldSince = -1f;

    void Start()
    {
        if (footsteps != null) footsteps.onSequenceFinished.AddListener(Begin);
        if (humEmitters == null || humEmitters.Length == 0) humEmitters = FindObjectsOfType<FridgeHum>(true);

        foreach (var l in lights) if (l != null) normalIntensity[l] = l.intensity;
        foreach (var l in restoredLights) if (l != null) normalIntensity[l] = l.intensity;
        foreach (var r in emissiveRenderers)
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_EmissionColor")) normalEmission[r] = r.sharedMaterial.GetColor("_EmissionColor");
        foreach (var l in restoredLights) if (l != null) l.enabled = false;
        if (fusebox != null)
        {
            fusebox.onEnergyRestored.AddListener(RestorePartialPower);
            fusebox.useBlocked = true;     // the fuse can only go in after the blackout
        }
    }

    void Update()
    {
        if (!blackoutWhenPlayerReachesFusebox || PowerIsOut || fusebox == null) return;
        var hunt = FuseHuntManager.instance;
        bool hasFuse = (hunt != null && hunt.FusePickedUp) || (Player.instance != null && Player.instance.inventory != null && Player.instance.inventory.HasFuse());
        if (!hasFuse) { fuseHeldSince = -1f; return; }
        if (fuseHeldSince < 0f) fuseHeldSince = Time.time;
        if (expedited) return;

        // the same moment 'Press I to open inventory' would show up: the player is in the fuse box's range
        bool atFusebox = Player.instance != null && Player.instance.fuseBoxInRange && Player.instance.currentFuseboxInRange == fusebox;
        if (atFusebox) Expedite("the player reached the fuse box with the fuse");
        else if (Time.time - fuseHeldSince >= failsafeSecondsAfterFuse) Expedite("the player has had the fuse for " + failsafeSecondsAfterFuse + " s");
    }

    /// <summary>Makes the blackout happen now-ish: skips whatever is left of the Runner scene, locks the door and cuts the power quickly.</summary>
    public void Expedite(string why)
    {
        if (expedited || PowerIsOut) return;
        expedited = true;
        Debug.Log("[StoreBlackout] EXPEDITED - " + why);
        if (Started)
        {
            delayLeft = Mathf.Min(delayLeft, expediteDelay);   // already counting down: just shorten it
            return;
        }
        if (footsteps != null) footsteps.AbortAndLock();       // locks the entrance -> onDoorLocked -> Begin()
        if (!Started) Begin();
    }

    void OnDestroy()
    {
        if (footsteps != null) footsteps.onSequenceFinished.RemoveListener(Begin);
        if (fusebox != null) fusebox.onEnergyRestored.RemoveListener(RestorePartialPower);
    }

    void LateUpdate()
    {
        if (Mathf.Approximately(ambientTarget, 1f) && ambientK >= 0.999f) { ambientK = 1f; haveWrite = false; return; }

        // if the sky system wrote new ambient values since our last write, take those as the new baseline
        bool untouched = haveWrite && RenderSettings.ambientLight == wroteLight && RenderSettings.ambientSkyColor == wroteSky
            && RenderSettings.ambientEquatorColor == wroteEquator && RenderSettings.ambientGroundColor == wroteGround;
        if (!untouched)
        {
            baseLight = RenderSettings.ambientLight; baseSky = RenderSettings.ambientSkyColor;
            baseEquator = RenderSettings.ambientEquatorColor; baseGround = RenderSettings.ambientGroundColor;
        }

        ambientK = Mathf.MoveTowards(ambientK, ambientTarget, Time.deltaTime / Mathf.Max(0.01f, ambientFadeSeconds));
        RenderSettings.ambientLight = wroteLight = baseLight * ambientK;
        RenderSettings.ambientSkyColor = wroteSky = baseSky * ambientK;
        RenderSettings.ambientEquatorColor = wroteEquator = baseEquator * ambientK;
        RenderSettings.ambientGroundColor = wroteGround = baseGround * ambientK;
        haveWrite = true;
    }

    [ContextMenu("Begin now")]
    public void Begin()
    {
        if (Started) return;
        Started = true;
        delayLeft = expedited ? expediteDelay : blackoutDelay;
        StartCoroutine(Co_Run());
    }

    IEnumerator Co_Run()
    {
        Debug.Log("[StoreBlackout] Runner sequence over - power fails in " + delayLeft + "s");
        while (delayLeft > 0f) { delayLeft -= Time.deltaTime; yield return null; }

        // --- power cut ---
        Play(powerCutClip, powerCutPoint != null ? powerCutPoint.position : transform.position, powerCutVolume, flatLayerVolume, 40f);
        foreach (var h in humEmitters) if (h != null) h.SetPowered(false);
        StartCoroutine(Co_FadeAmbience());
        yield return Co_LightsOut();
        ambientTarget = blackoutAmbientScale;
        PowerIsOut = true;
        Debug.Log("[StoreBlackout] power is out - " + silenceSeconds + "s of silence");
        onBlackout?.Invoke();

        // --- silence ---
        yield return new WaitForSeconds(silenceSeconds);

        // --- crash ---
        Vector3 at = crashPoint != null ? crashPoint.position : transform.position;
        Play(crashClip, at, crashVolume, flatLayerVolume, crashMaxDistance);
        Debug.Log("[StoreBlackout] CRASH");
        onCrash?.Invoke();
        if (collapseLead > 0f) yield return new WaitForSeconds(collapseLead);
        if (collapse != null) collapse.Collapse();

        Finished = true;
        onFinished?.Invoke();

        // only now may the fuse go in
        yield return new WaitForSeconds(fuseUsableAfterCrash);
        if (fusebox != null) fusebox.useBlocked = false;
        Debug.Log("[StoreBlackout] the fuse can be installed now");
    }

    IEnumerator Co_LightsOut()
    {
        if (stutter != null)
        {
            foreach (var s in stutter)
            {
                SetLights(false);
                yield return new WaitForSeconds(s.x);
                SetLights(true);
                yield return new WaitForSeconds(s.y);
            }
        }
        SetLights(false);
        // the old flicker script re-enables things when it is disabled, so switch it off for good
        foreach (var l in lights)
        {
            if (l == null) continue;
            var f = l.GetComponent<LightFlicker>();
            if (f != null) f.enabled = false;
            l.enabled = false;
            l.gameObject.SetActive(false);
        }
        foreach (var fx in fixtures) if (fx != null) fx.Cut();
    }

    void SetLights(bool on)
    {
        foreach (var l in lights) if (l != null) l.enabled = on;
        SetEmission(on);
        foreach (var f in fixtures) if (f != null) f.StutterSet(on);
    }

    void SetEmission(bool on)
    {
        var mpb = new MaterialPropertyBlock();
        foreach (var r in emissiveRenderers)
        {
            if (r == null) continue;
            if (on) { r.SetPropertyBlock(null); continue; }
            mpb.Clear();
            mpb.SetColor("_EmissionColor", Color.black);
            r.SetPropertyBlock(mpb);
        }
    }

    IEnumerator Co_FadeAmbience()
    {
        if (ambienceToFade == null || ambienceToFade.Length == 0) yield break;
        var start = new float[ambienceToFade.Length];
        for (int i = 0; i < start.Length; i++) start[i] = ambienceToFade[i] != null ? ambienceToFade[i].volume : 0f;
        float t = 0f;
        while (t < ambienceFadeSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, ambienceFadeSeconds));
            for (int i = 0; i < start.Length; i++) if (ambienceToFade[i] != null) ambienceToFade[i].volume = Mathf.Lerp(start[i], 0f, k);
            yield return null;
        }
    }

    /// <summary>
    /// The fuse is in: the lights come back dimly (storage-room lights and the store ceiling lights, not the sun light), the
    /// fridges start humming again. Does nothing unless the power was cut.
    /// </summary>
    public void RestorePartialPower()
    {
        if (!PowerIsOut || partiallyRestored) return;
        partiallyRestored = true;
        if (powerOnClip != null)
            Play(powerOnClip, powerCutPoint != null ? powerCutPoint.position : transform.position, powerOnVolume, flatLayerVolume, 40f);
        StartCoroutine(Co_PartialRestore());
    }

    bool partiallyRestored;

    IEnumerator Co_PartialRestore()
    {
        var toRestore = new List<Light>();
        foreach (var l in lights) if (l != null && l.GetComponent<LightFlicker>() == null) toRestore.Add(l);
        foreach (var l in restoredLights) if (l != null) toRestore.Add(l);
        foreach (var l in toRestore) { l.gameObject.SetActive(true); l.intensity = normalIntensity[l] * restoredLightLevel; }

        ambientTarget = restoredAmbientScale;
        foreach (var h in humEmitters) if (h != null) h.SetPowered(true);
        PowerIsOut = false;

        bool haveFixtures = fixtures != null && fixtures.Length > 0;
        if (haveFixtures) foreach (var fx in fixtures) if (fx != null) fx.Restore();

        if (!haveFixtures && restoreStutter != null)
        {
            foreach (var st in restoreStutter)
            {
                yield return new WaitForSeconds(st.x);
                SetRestoredLights(toRestore, true);
                yield return new WaitForSeconds(st.y);
                SetRestoredLights(toRestore, false);
            }
        }
        if (!haveFixtures) SetRestoredLights(toRestore, true);
        Debug.Log("[StoreBlackout] fuse in - lights back at " + restoredLightLevel + " (ambient x" + restoredAmbientScale + ")");
    }

    void SetRestoredLights(List<Light> list, bool on)
    {
        foreach (var l in list) l.enabled = on;
        var mpb = new MaterialPropertyBlock();
        foreach (var r in emissiveRenderers)
        {
            if (r == null) continue;
            mpb.Clear();
            Color c = normalEmission.TryGetValue(r, out var e) ? e : Color.white;
            mpb.SetColor("_EmissionColor", on ? c * restoredLightLevel : Color.black);
            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>Brings the power back (for the fuse phase): lights, emission, fridge hums and ambient light.</summary>
    public void RestorePower()
    {
        ambientTarget = 1f;
        PowerIsOut = false;
        foreach (var l in lights)
        {
            if (l == null) continue;
            l.gameObject.SetActive(true);
            l.enabled = true;
            var f = l.GetComponent<LightFlicker>();
            if (f != null) f.enabled = true;
        }
        SetEmission(true);
        foreach (var fx in fixtures) if (fx != null) fx.SetMode(FluorescentFixture.Mode.On, true);
        foreach (var h in humEmitters) if (h != null) h.SetPowered(true);
    }

    // ---- audio helpers ----

    void Play(AudioClip clip, Vector3 pos, float volume3D, float volumeFlat, float maxDistance)
    {
        if (clip == null) return;
        var a = GetSource();
        a.transform.position = pos;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 4f;
        a.maxDistance = maxDistance;
        a.volume = volume3D;
        a.pitch = 1f;
        a.clip = clip;
        a.Play();

        if (volumeFlat > 0f)
        {
            if (cam == null) cam = Camera.main;
            float d = cam != null ? Vector3.Distance(cam.transform.position, pos) : hearRange;
            float v = volumeFlat * Mathf.Clamp01(1f - d / Mathf.Max(1f, hearRange));
            if (v > 0.001f)
            {
                var f = GetSource();
                f.transform.position = pos;
                f.spatialBlend = 0f;
                f.volume = v;
                f.pitch = 1f;
                f.clip = clip;
                f.Play();
            }
        }
    }

    AudioSource GetSource()
    {
        foreach (var s in pool) if (s != null && !s.isPlaying) return s;
        var go = new GameObject("BlackoutSfx");
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.dopplerLevel = 0f;
        a.outputAudioMixerGroup = sfxGroup;
        pool.Add(a);
        return a;
    }
}
