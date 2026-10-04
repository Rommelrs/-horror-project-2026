using System.Collections;
using UnityEngine;

/// <summary>
/// Looping 3D hum for fridges, freezers and vending machines. Put it on a child object that also has an AudioSource
/// (see the FridgeHum prefab). Each hum starts at a random point of the loop so several machines do not phase together.
/// Call SetPowered(false) from a UnityEvent to cut the hum (fades out), SetPowered(true) to bring it back.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FridgeHum : MonoBehaviour
{
    [Header("Variation")]
    [SerializeField] bool randomStartTime = true;
    [Tooltip("Random pitch multiplier range, so neighbouring machines do not hum in perfect unison.")]
    [SerializeField] Vector2 pitchRange = new Vector2(0.97f, 1.03f);

    [Header("Power")]
    [SerializeField] bool poweredOnStart = true;
    [SerializeField] float fadeSeconds = 1.5f;
    [Tooltip("The hum winds down to this fraction of its pitch while it dies.")]
    [SerializeField] float powerDownPitch = 0.55f;

    AudioSource source;
    float fullVolume;
    float basePitch;
    Coroutine fadeCR;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        fullVolume = source.volume;
        basePitch = Random.Range(pitchRange.x, pitchRange.y);
        source.pitch = basePitch;
    }

    void OnEnable()
    {
        if (poweredOnStart)
            StartHum(false);
    }

    public void SetPowered(bool on)
    {
        if (on) StartHum(true);
        else StopHum(true);
    }

    void StartHum(bool fade)
    {
        if (source.clip == null) return;
        if (!source.isPlaying)
        {
            if (randomStartTime) source.time = Random.Range(0f, source.clip.length * 0.99f);
            source.volume = fade ? 0f : fullVolume;
            source.pitch = basePitch;
            source.Play();
        }
        if (fade) Fade(fullVolume, false);
        else source.volume = fullVolume;
    }

    void StopHum(bool fade)
    {
        if (!source.isPlaying) return;
        if (fade && isActiveAndEnabled) Fade(0f, true);
        else source.Stop();
    }

    void Fade(float target, bool stopAtEnd)
    {
        if (fadeCR != null) StopCoroutine(fadeCR);
        fadeCR = StartCoroutine(Co_Fade(target, stopAtEnd));
    }

    IEnumerator Co_Fade(float target, bool stopAtEnd)
    {
        float start = source.volume;
        float startPitch = source.pitch;
        float targetPitch = stopAtEnd ? basePitch * powerDownPitch : basePitch;
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeSeconds));
            source.volume = Mathf.Lerp(start, target, k);
            source.pitch = Mathf.Lerp(startPitch, targetPitch, k);
            yield return null;
        }
        source.volume = target;
        source.pitch = targetPitch;
        if (stopAtEnd) source.Stop();
        fadeCR = null;
    }
}
