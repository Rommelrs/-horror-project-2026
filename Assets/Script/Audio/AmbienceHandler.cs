using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmbienceHandler : MonoBehaviour
{
    public static AmbienceHandler Instance;

    public enum AmbienceType
    {
        None,
        Outdoor,
        Store
    }

    [System.Serializable]
    public struct AmbienceData
    {
        public AmbienceType ambienceType;
        public AudioClip clip1;
        [Range(0f, 1f)] [SerializeField] float clip1Volume;
        public AudioClip clip2;
        [Range(0f, 1f)] [SerializeField] float clip2Volume;

        public float GetClip1Volume()
        {
            return clip1Volume * Instance.volumeOptionMultiplier;
        }

        public float GetClip2Volume()
        {
            return clip2Volume * Instance.volumeOptionMultiplier;
        }
    }

    [Header("Audio Sources")]
    [SerializeField] AudioSource currentSource1;
    [SerializeField] AudioSource currentSource2;
    [SerializeField] AudioSource nextSource1;
    [SerializeField] AudioSource nextSource2;

    [Header("Settings")]
    [SerializeField] AmbienceType defaultAmbienceType;
    [SerializeField] float crossfadeDuration = 3f;
    [SerializeField] float fadeOutDuration = 1f;

    [Header("Ambience Data")]
    [SerializeField] AmbienceData[] ambienceData;

    AmbienceType currentAmbience;
    Coroutine crossfadeRoutine;
    bool busyCrossFading = false;
    Coroutine updateAmbienceVolume;

    float volumeOptionMultiplier = 1f;

    private void Awake()
    {
        Instance = this;

        SetupSource(currentSource1);
        SetupSource(currentSource2);
        SetupSource(nextSource1);
        SetupSource(nextSource2);
    }

    private void Start()
    {
        //Play Default Ambience
        if (currentAmbience == AmbienceType.None)
            UpdateAmbience(defaultAmbienceType);
    }

    public void UpdateAmbience(AmbienceType newAmbience)
    {
        if (newAmbience == currentAmbience)
            return;

        AmbienceData data = GetAmbienceData(newAmbience);
        if (data.clip1 == null && data.clip2 == null)
            return;

        currentAmbience = newAmbience;

        if (updateAmbienceVolume != null) StopCoroutine(updateAmbienceVolume);

        if (crossfadeRoutine != null) StopCoroutine(crossfadeRoutine);
        crossfadeRoutine = StartCoroutine(CrossfadeAmbience(data));
    }

    public void UpdateAmbienceVolume(float volumeMultiplier)
    {
        //Debug.LogError("Set New AmbienceVolume: " + volumeMultiplier);

        AmbienceData data = GetAmbienceData(currentAmbience);
        if (data.clip1 == null && data.clip2 == null)
            return;

        volumeOptionMultiplier = volumeMultiplier;

        if (updateAmbienceVolume != null) StopCoroutine(updateAmbienceVolume);
        updateAmbienceVolume = StartCoroutine(Co_UpdateAmbienceVolume(data));
    }

    //Smoothly Lower the volume of all ambience music and stop playing
    public void FadeOutAmbience()
    {
        if (crossfadeRoutine != null) StopCoroutine(crossfadeRoutine);
        crossfadeRoutine = StartCoroutine(Co_FadeOutAmbience());
    }

    IEnumerator Co_FadeOutAmbience()
    {
        busyCrossFading = true;

        float time = 0f;
        float currentSource1Volume = currentSource1.volume;
        float currentSource2Volume = currentSource2.volume;

        while (time < crossfadeDuration)
        {
            float t = time / crossfadeDuration;

            //Fade out
            currentSource1.volume = Mathf.Lerp(currentSource1Volume, 0f, t);
            currentSource2.volume = Mathf.Lerp(currentSource2Volume, 0f, t);

            time += Time.deltaTime;
            yield return null;
        }

        //Stopambience
        currentSource1.Stop();
        currentSource2.Stop();

        currentAmbience = AmbienceType.None;

        busyCrossFading = false;
    }

    #region Core Logic
    IEnumerator CrossfadeAmbience(AmbienceData data)
    {
        busyCrossFading = true;
        // Setup next ambience
        SetupClip(nextSource1, data.clip1);
        SetupClip(nextSource2, data.clip2);

        float time = 0f;
        float currentSource1Volume = currentSource1.volume;
        float currentSource2Volume = currentSource2.volume;

        while (time < crossfadeDuration)
        {
            float t = time / crossfadeDuration;

            // Fade in
            nextSource1.volume = Mathf.Lerp(0f, data.GetClip1Volume(), t);
            nextSource2.volume = Mathf.Lerp(0f, data.GetClip2Volume(), t);

            // Fade out
            currentSource1.volume = Mathf.Lerp(currentSource1Volume, 0f, t);
            currentSource2.volume = Mathf.Lerp(currentSource2Volume, 0f, t);

            time += Time.deltaTime;
            yield return null;
        }

        // Stop old ambience
        currentSource1.Stop();
        currentSource2.Stop();

        SwapSources();
        busyCrossFading = false;
    }

    IEnumerator Co_UpdateAmbienceVolume(AmbienceData data)
    {
        while (busyCrossFading)
        {
            yield return null;
        }

        float currentSource1Volume = currentSource1.volume;
        float currentSource2Volume = currentSource2.volume;
        float newSource1Volume = data.GetClip1Volume();
        float newSource2Volume = data.GetClip2Volume();

        float time = 0f;

        while (time < fadeOutDuration)
        {
            float t = time / fadeOutDuration;

            // Fade in
            currentSource1.volume = Mathf.Lerp(currentSource1Volume, newSource1Volume, t);
            currentSource2.volume = Mathf.Lerp(currentSource2Volume, newSource2Volume, t);

            time += Time.deltaTime;
            yield return null;
        }
    }
    #endregion

    #region Helpers
    void SetupSource(AudioSource source)
    {
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    void SetupClip(AudioSource source, AudioClip clip)
    {
        if (source == null)
            return;

        if (clip == null)
            return;

        source.clip = clip;
        source.volume = 0f;
        source.Play();
    }

    void SwapSources()
    {
        (currentSource1, nextSource1) = (nextSource1, currentSource1);
        (currentSource2, nextSource2) = (nextSource2, currentSource2);
    }

    AmbienceData GetAmbienceData(AmbienceType type)
    {
        foreach (var data in ambienceData)
        {
            if (data.ambienceType == type)
                return data;
        }

        Debug.LogWarning($"No ambience data found for {type}");
        return default;
    }

    #endregion
}
