using UnityEngine;

/// <summary>
/// The little status lamp on the fuse box: red while it has no power (a slow blink, the way a fault lamp does, with a
/// small red glow on the wall so it is found in the dark), steady green once the fuse is in.
/// </summary>
public class FuseboxStatusLight : MonoBehaviour
{
    [SerializeField] Fusebox fusebox;
    [Tooltip("The lamp's lens (its material gets an emission colour at runtime).")]
    [SerializeField] Renderer lens;
    [Tooltip("A small point light just in front of the lens.")]
    [SerializeField] Light glow;
    [ColorUsage(false, true)] public Color faultColor = new Color(3.2f, 0.12f, 0.08f);
    [ColorUsage(false, true)] public Color okColor = new Color(0.15f, 2.2f, 0.35f);
    public float faultLightIntensity = 1.1f;
    public float okLightIntensity = 0.35f;
    [Tooltip("Fault blink: seconds per cycle (0 = steady).")]
    public float blinkSeconds = 1.6f;

    Material mat;

    void Start()
    {
        if (lens != null) mat = lens.material;
        if (mat != null) { mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
    }

    void Update()
    {
        bool ok = fusebox != null && fusebox.hasEnergy;
        float k = 1f;
        if (!ok && blinkSeconds > 0f)
        {
            // mostly on, a short dip: reads as a fault lamp, never fully gone
            float ph = Mathf.Repeat(Time.time / blinkSeconds, 1f);
            k = ph < 0.72f ? 1f : 0.18f;
        }
        Color c = ok ? okColor : faultColor;
        if (mat != null) mat.SetColor("_EmissionColor", c * k);
        if (glow != null)
        {
            glow.color = ok ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.12f, 0.08f);
            glow.intensity = (ok ? okLightIntensity : faultLightIntensity) * k;
        }
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
