using System.Collections;
using UnityEngine;

public class LightFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    [SerializeField] float minIntensity = 0.5f;
    [SerializeField] float maxIntensity = 1.5f;
    [SerializeField] float flickerSpeed = 0.1f;

    [Header("Audio")]
    [SerializeField] AudioSource flickerAudioSource;
    [SerializeField] AudioClip flickerLoopClip;

    [Header("Optional")]
    [SerializeField] bool flickerOnStart = true;

    Light lightSource;
    Coroutine flickerCR;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
    }

    private void Start()
    {
        if (flickerOnStart)
            StartFlicker();
    }

    public void StartFlicker()
    {
        if (flickerCR != null) StopCoroutine(flickerCR);
        flickerCR = StartCoroutine(Co_Flicker());

        if (flickerAudioSource != null && flickerLoopClip != null)
        {
            flickerAudioSource.clip = flickerLoopClip;
            flickerAudioSource.loop = true;
            flickerAudioSource.Play();
        }
    }

    public void StopFlicker()
    {
        if (flickerCR != null) StopCoroutine(flickerCR);

        if (flickerAudioSource != null && flickerAudioSource.isPlaying)
            flickerAudioSource.Stop();
    }

    IEnumerator Co_Flicker()
    {
        while (true)
        {
            lightSource.intensity = Random.Range(minIntensity, maxIntensity);
            yield return new WaitForSeconds(flickerSpeed);
        }
    }
}
