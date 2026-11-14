using UnityEngine;

/// <summary>
/// Makes the cabinet that holds the fuse stand out in the dark store: its own surfaces glow faintly (so its shape reads
/// against the blackout) with a slow breathing pulse, and dust glints in front of it like on the other things that can
/// be used. All of it stops once the fuse has been taken. Also runs the battery emergency lamp on the wall above it: the
/// one light in the shop while the power is out, shining on this cabinet.
/// </summary>
public class FuseCabinetHighlight : MonoBehaviour
{
    [Tooltip("The cabinet's surfaces (they get their own material copies at runtime).")]
    public Renderer[] renderers;
    public ParticleSystem dust;
    [Tooltip("How bright the surfaces glow (their own texture, times this).")]
    public Color glow = new Color(0.34f, 0.31f, 0.26f);
    [Range(0f, 1f)] public float pulse = 0.4f;
    public float pulseSeconds = 2.6f;

    [Header("Emergency lamp on the wall above (on while the power is out)")]
    public Light lamp;
    public Renderer lampLens;
    public StoreBlackoutSequence blackout;
    public float lampIntensity = 3.2f;
    [ColorUsage(false, true)] public Color lensGlow = new Color(2.2f, 2.0f, 1.6f);
    [Tooltip("A tired battery: how deep the light sags now and then (0 = steady).")]
    [Range(0f, 1f)] public float lampSag = 0.35f;

    Material[] mats;
    Material lensMat;
    float level, lampLevel, sagUntil, nextSag, sagDepth;

    void Start()
    {
        var list = new System.Collections.Generic.List<Material>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var m in r.materials)
            {
                if (m == null || !m.HasProperty("_EmissionColor")) continue;
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionMap") && m.mainTexture != null) m.SetTexture("_EmissionMap", m.mainTexture);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", Color.black);
                list.Add(m);
            }
        }
        mats = list.ToArray();
        level = Wanted() ? 1f : 0f;
        Apply();
        if (lampLens != null) lensMat = lampLens.material;
        nextSag = Time.time + 4f;
        ApplyLamp();
    }

    /// <summary>The emergency lamp: comes on with a stutter when the power goes, sags like a tired battery, off when power is back.</summary>
    void ApplyLamp()
    {
        if (lamp == null) return;
        bool on = blackout != null && blackout.PowerIsOut;
        lampLevel = Mathf.MoveTowards(lampLevel, on ? 1f : 0f, Time.deltaTime / (on ? 1.1f : 0.25f));
        float k = lampLevel;
        if (on && lampLevel < 1f) k *= Mathf.PerlinNoise(Time.time * 23f, 3.1f) > 0.42f ? 1f : 0.15f;      // strikes up
        if (on && lampLevel >= 1f)
        {
            if (Time.time >= nextSag) { sagUntil = Time.time + Random.Range(0.25f, 0.9f); sagDepth = Random.Range(0.4f, 1f) * lampSag; nextSag = sagUntil + Random.Range(3.5f, 9f); }
            if (Time.time < sagUntil) k *= 1f - sagDepth * (0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 17f, 7.7f));
            k *= 0.96f + 0.04f * Mathf.PerlinNoise(Time.time * 6f, 1.3f);
        }
        lamp.enabled = k > 0.01f;
        lamp.intensity = lampIntensity * k;
        if (lensMat != null) lensMat.SetColor("_EmissionColor", lensGlow * k);
    }

    static bool Wanted()
    {
        var hunt = FuseHuntManager.instance;
        return hunt == null || !hunt.FusePickedUp;
    }

    void Update()
    {
        ApplyLamp();
        if (mats == null) return;
        bool on = Wanted();
        level = Mathf.MoveTowards(level, on ? 1f : 0f, Time.deltaTime / 1.2f);
        Apply();
        if (dust != null)
        {
            var em = dust.emission;
            if (em.enabled != on) em.enabled = on;
        }
    }

    void Apply()
    {
        float wave = 1f - pulse * (0.5f + 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / Mathf.Max(0.2f, pulseSeconds)));
        Color c = glow * (level * wave);
        foreach (var m in mats) if (m != null) m.SetColor("_EmissionColor", c);
    }

    void OnDestroy()
    {
        if (mats != null) foreach (var m in mats) if (m != null) Destroy(m);
        if (lensMat != null) Destroy(lensMat);
    }
}
