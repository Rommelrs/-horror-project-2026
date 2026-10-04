using System.Collections;
using UnityEngine;

public class LightFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    [SerializeField] float minIntensity = 0.5f;
    [SerializeField] float maxIntensity = 1.5f;
    [SerializeField] float flickerSpeed = 0.1f;

    [Header("Occasional Flicker")]
    [Tooltip("If on, the light stays steady and only flickers in short bursts. If off, it flickers continuously.")]
    [SerializeField] bool occasional = true;
    [Tooltip("Seconds the light stays steady between bursts (random in this range).")]
    [SerializeField] Vector2 steadyDuration = new Vector2(5f, 14f);
    [Tooltip("Seconds each burst of flickering lasts (random in this range).")]
    [SerializeField] Vector2 burstDuration = new Vector2(0.4f, 1.4f);

    [Header("Audio")]
    [SerializeField] AudioSource flickerAudioSource;
    [SerializeField] AudioClip flickerLoopClip;

    [Header("Optional")]
    [SerializeField] bool flickerOnStart = true;

    Light lightSource;
    Coroutine flickerCR;
    float steadyIntensity;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        steadyIntensity = lightSource.intensity;
    }

    private void Start()
    {
        if (flickerOnStart)
            StartFlicker();
    }

    private void OnDisable()
    {
        StopFlicker();
    }

    public void StartFlicker()
    {
        if (flickerCR != null) StopCoroutine(flickerCR);
        flickerCR = StartCoroutine(occasional ? Co_OccasionalFlicker() : Co_ContinuousFlicker());
    }

    public void StopFlicker()
    {
        if (flickerCR != null) StopCoroutine(flickerCR);
        flickerCR = null;
        StopFlickerAudio();
        if (lightSource != null) lightSource.intensity = steadyIntensity;
    }

    void PlayFlickerAudio()
    {
        if (flickerAudioSource == null || flickerLoopClip == null) return;
        flickerAudioSource.clip = flickerLoopClip;
        flickerAudioSource.loop = true;
        if (!flickerAudioSource.isPlaying) flickerAudioSource.Play();
    }

    void StopFlickerAudio()
    {
        if (flickerAudioSource != null && flickerAudioSource.isPlaying)
            flickerAudioSource.Stop();
    }

    IEnumerator Co_ContinuousFlicker()
    {
        PlayFlickerAudio();
        while (true)
        {
            lightSource.intensity = Random.Range(minIntensity, maxIntensity);
            yield return new WaitForSeconds(flickerSpeed);
        }
    }

    IEnumerator Co_OccasionalFlicker()
    {
        lightSource.intensity = steadyIntensity;
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(steadyDuration.x, steadyDuration.y));

            PlayFlickerAudio();
            float end = Time.time + Random.Range(burstDuration.x, burstDuration.y);
            while (Time.time < end)
            {
                lightSource.intensity = Random.Range(minIntensity, maxIntensity);
                yield return new WaitForSeconds(flickerSpeed);
            }

            lightSource.intensity = steadyIntensity;
            StopFlickerAudio();
        }
    }
}
