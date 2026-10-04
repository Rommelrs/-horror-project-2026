using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// One ceiling fluorescent fixture: glowing tube(s), an optional real light and an electrical buzz.
///  - Mirror: the tube follows the store's main light (the Enviro sun that flickers occasionally); no light of its own.
///  - Off:    dark and silent.
///  - On:     steady, with a short ballast stutter when it comes on.
///  - Flicker: unreliable - mostly dim, with bursts of flicker and the odd moment of darkness.
/// StoreBlackoutSequence puts every fixture in Off at the power cut and into its own 'restoredMode' when the fuse goes in.
/// </summary>
public class FluorescentFixture : MonoBehaviour
{
    public enum Mode { Mirror, Off, On, Flicker }

    [Header("Parts")]
    [SerializeField] Light lamp;
    [SerializeField] Renderer[] tubes;
    [SerializeField] AudioSource buzz;
    [Tooltip("The light the tube mirrors in Mirror mode (the Enviro sun with the LightFlicker on it).")]
    [SerializeField] Light mirrorLight;

    [Tooltip("In Mirror mode the real light is on at full (for fixtures whose lamp lights the room before the blackout, like the storage room). Off: only the tube glows and the sun does the lighting.")]
    [SerializeField] bool lampOnInMirror = false;

    [Header("Modes")]
    public Mode mode = Mode.Mirror;
    [Tooltip("What this fixture does when the power comes back.")]
    public Mode restoredMode = Mode.On;
    [Tooltip("Real light output (fraction of the lamp's normal intensity) when On / when Flicker is at full.")]
    [SerializeField, Range(0f, 1f)] float onLevel = 0.5f;
    [SerializeField, Range(0f, 1f)] float flickerLevel = 0.6f;
    [Tooltip("Tube glow when On.")]
    [SerializeField, Range(0f, 2f)] float onGlow = 0.9f;

    [Header("Audio")]
    [SerializeField] AudioClip buzzClip;
    [SerializeField] AudioClip tickClip;
    [SerializeField, Range(0f, 1f)] float buzzVolume = 0.12f;
    [SerializeField] AudioMixerGroup sfxGroup;

    Material[] mats;
    Color[] baseEmission;
    float lampBase = 1f;
    float mirrorBase = 1f;
    float stutterOverride = -1f;
    Coroutine modeCR;
    float glow;          // current tube glow factor
    float lampK;         // current real-light factor

    public Mode Current { get { return mode; } }

    void Awake()
    {
        if (lamp != null) lampBase = lamp.intensity;
        if (mirrorLight != null) mirrorBase = Mathf.Max(0.01f, mirrorLight.intensity);

        // every glowing material on the tube renderers (own instances, so one fixture can go dark on its own)
        var matList = new System.Collections.Generic.List<Material>();
        var emList = new System.Collections.Generic.List<Color>();
        if (tubes != null)
        {
            foreach (var r in tubes)
            {
                if (r == null) continue;
                foreach (var m in r.materials)
                {
                    if (m == null || !m.HasProperty("_EmissionColor")) continue;
                    Color c = m.GetColor("_EmissionColor");
                    if (c.maxColorComponent < 0.05f) continue;      // not a glowing part (end caps, frame)
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    matList.Add(m); emList.Add(c);
                }
            }
        }
        mats = matList.ToArray();
        baseEmission = emList.ToArray();

        if (buzz == null)
        {
            buzz = gameObject.AddComponent<AudioSource>();
        }
        buzz.playOnAwake = false; buzz.loop = true; buzz.spatialBlend = 1f;
        buzz.rolloffMode = AudioRolloffMode.Linear; buzz.minDistance = 1.5f; buzz.maxDistance = 11f; buzz.dopplerLevel = 0f;
        buzz.outputAudioMixerGroup = sfxGroup;
        buzz.clip = buzzClip;
        buzz.volume = 0f;
    }

    void OnEnable() { SetMode(mode, false); }

    void Update()
    {
        if (mode != Mode.Mirror) return;
        float k = 0f;
        if (stutterOverride >= 0f) k = stutterOverride;
        else if (mirrorLight != null && mirrorLight.gameObject.activeInHierarchy && mirrorLight.enabled) k = Mathf.Clamp01(mirrorLight.intensity / mirrorBase);
        else if (mirrorLight == null) k = 1f;
        float lk = lampOnInMirror ? (stutterOverride >= 0f ? stutterOverride : 1f) : 0f;
        Apply(k * onGlow, lk);
    }

    /// <summary>Instant on/off while the power is failing (follows the stutter of the blackout).</summary>
    public void StutterSet(bool on)
    {
        stutterOverride = on ? 1f : 0f;
        if (mode != Mode.Mirror) { mode = Mode.Mirror; StopMode(); }
    }

    /// <summary>The power is gone.</summary>
    public void Cut()
    {
        stutterOverride = -1f;
        SetMode(Mode.Off, false);
    }

    /// <summary>The power is partly back: this fixture does what its 'restoredMode' says.</summary>
    public void Restore()
    {
        SetMode(restoredMode, true);
    }

    public void SetMode(Mode m, bool startup)
    {
        stutterOverride = -1f;
        mode = m;
        StopMode();
        switch (m)
        {
            case Mode.Off: Apply(0f, 0f); break;
            case Mode.On: modeCR = StartCoroutine(Co_On(startup)); break;
            case Mode.Flicker: modeCR = StartCoroutine(Co_Flicker(startup)); break;
            default: break;   // Mirror: Update does it
        }
    }

    void StopMode()
    {
        if (modeCR != null) StopCoroutine(modeCR);
        modeCR = null;
    }

    // ---------------------------------------------------------------- behaviours

    IEnumerator Co_On(bool startup)
    {
        if (startup)
        {
            Apply(0f, 0f);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.8f));   // fixtures do not all start together
            Tick();
            for (int i = 0; i < 3; i++)
            {
                Apply(onGlow * Random.Range(0.4f, 1f), onLevel * 0.5f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
                Apply(0f, 0f);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
        }
        Apply(onGlow, onLevel);
    }

    IEnumerator Co_Flicker(bool startup)
    {
        if (startup) { Apply(0f, 0f); yield return new WaitForSeconds(Random.Range(0.2f, 1.2f)); Tick(); }
        while (true)
        {
            // dim and unsteady for a while ...
            float calm = Random.Range(1.2f, 3.5f);
            float t = 0f;
            while (t < calm)
            {
                float w = 0.55f + 0.1f * Mathf.Sin(Time.time * 7f);
                Apply(onGlow * w, flickerLevel * w);
                t += Time.deltaTime; yield return null;
            }
            // ... then a burst of flicker
            Tick();
            float burst = Random.Range(0.35f, 1.1f);
            t = 0f;
            while (t < burst)
            {
                float k = Random.value < 0.35f ? 0f : Random.Range(0.3f, 1f);
                Apply(onGlow * k, flickerLevel * k);
                float step = Random.Range(0.03f, 0.1f);
                t += step; yield return new WaitForSeconds(step);
            }
            // and sometimes it just dies for a moment
            if (Random.value < 0.4f) { Apply(0f, 0f); yield return new WaitForSeconds(Random.Range(0.4f, 1.1f)); Tick(); }
        }
    }

    // ---------------------------------------------------------------- output

    void Apply(float glowK, float lightK)
    {
        glow = glowK; lampK = lightK;
        if (mats != null)
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null) mats[i].SetColor("_EmissionColor", baseEmission[i] * glowK);

        if (lamp != null)
        {
            bool on = lightK > 0.01f;
            if (on && !lamp.gameObject.activeSelf) lamp.gameObject.SetActive(true);
            lamp.enabled = on;
            lamp.intensity = lampBase * lightK;
        }

        if (buzz != null && buzzClip != null)
        {
            float v = Mathf.Clamp01(glowK / Mathf.Max(0.01f, onGlow)) * buzzVolume;
            if (v > 0.002f)
            {
                buzz.volume = v;
                if (!buzz.isPlaying) { buzz.time = Random.value * buzzClip.length * 0.9f; buzz.Play(); }
            }
            else if (buzz.isPlaying) buzz.Stop();
        }
    }

    void Tick()
    {
        if (tickClip == null || buzz == null) return;
        buzz.PlayOneShot(tickClip, Mathf.Min(1f, buzzVolume * 5f));
    }
}
