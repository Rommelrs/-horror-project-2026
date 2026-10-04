using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// The cash register comes to life when the fuse goes in: a relay click and a boot beep, the screen flickers on and
/// settles to a soft green glow with a small light on the counter. (The register's own logic already waits for
/// Fusebox.hasEnergy; this is only the visible/audible part.)
/// </summary>
public class CashRegisterPowerFX : MonoBehaviour
{
    [SerializeField] Fusebox fusebox;
    [SerializeField] Renderer screen;
    [SerializeField] Light glowLight;
    [SerializeField] AudioClip bootClip;
    [SerializeField] AudioMixerGroup sfxGroup;

    [Header("Look")]
    [SerializeField] Color screenColor = new Color(0.25f, 1f, 0.4f);
    [SerializeField] float screenGlow = 1.8f;
    [SerializeField] float lightIntensity = 0.5f;
    [Tooltip("Seconds after the fuse box fires its event before the register boots (the lights come on first).")]
    [SerializeField] float bootDelay = 1.2f;

    Material mat;
    AudioSource src;
    bool useEmission;
    Color baseColor = Color.white;

    void Awake()
    {
        if (screen != null)
        {
            mat = screen.material;
            useEmission = mat.HasProperty("_EmissionColor");
            if (useEmission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else if (mat.HasProperty("_Color")) baseColor = mat.GetColor("_Color");   // unlit screen: dark when off, its normal colour when on
        }
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1.5f; src.maxDistance = 16f; src.dopplerLevel = 0f; src.outputAudioMixerGroup = sfxGroup;
    }

    void Start()
    {
        bool powered = fusebox != null && fusebox.hasEnergy;
        Set(powered ? 1f : 0f);
        if (fusebox != null) fusebox.onEnergyRestored.AddListener(PowerOn);
    }

    void OnDestroy()
    {
        if (fusebox != null) fusebox.onEnergyRestored.RemoveListener(PowerOn);
    }

    [ContextMenu("Power on")]
    public void PowerOn()
    {
        StartCoroutine(Co_Boot());
    }

    IEnumerator Co_Boot()
    {
        yield return new WaitForSeconds(bootDelay);
        if (bootClip != null && screen != null)
        {
            src.transform.position = screen.bounds.center;
            src.PlayOneShot(bootClip, 0.8f);
        }
        // the screen flickers on
        float[] pattern = { 0.6f, 0f, 1f, 0.2f, 0f, 1f };
        foreach (float k in pattern)
        {
            Set(k);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.14f));
        }
        Set(1f);
    }

    void Set(float k)
    {
        if (mat != null)
        {
            if (useEmission) mat.SetColor("_EmissionColor", screenColor * screenGlow * k);
            else if (mat.HasProperty("_Color")) { Color c = baseColor * Mathf.Clamp01(k); c.a = baseColor.a; mat.SetColor("_Color", c); }
        }
        if (glowLight != null)
        {
            glowLight.color = screenColor;
            glowLight.intensity = lightIntensity * k;
            glowLight.enabled = k > 0.01f;
        }
    }
}
