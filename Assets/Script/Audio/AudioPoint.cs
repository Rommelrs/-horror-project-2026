using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ToolBox.Pools;

public class AudioPoint : MonoBehaviour, IPoolable
{
    public float lifeTime = 3f;

    AudioSource audioSource;
    Coroutine releaseCR;

    float defaultMinDistance, defaultMaxDistance;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        defaultMinDistance = audioSource.minDistance;
        defaultMaxDistance = audioSource.maxDistance;
    }

    public void OnDepool()
    {
        if (releaseCR != null) StopCoroutine(releaseCR);
        // pooled: undo any range a previous sound set
        audioSource.minDistance = defaultMinDistance;
        audioSource.maxDistance = defaultMaxDistance;
    }

    // Full volume inside minDistance, fading out to maxDistance. Applies to the next sound played on this source only.
    public void SetRange(float minDistance, float maxDistance)
    {
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
    }

    public void OnPool()
    {

    }

    public void PlayAudio(AudioClip audioClip, float volume, bool randomPitch = false)
    {
        audioSource.clip = audioClip;
        audioSource.volume = volume;

        if(randomPitch && SoundEffectManager.instance)
            audioSource.pitch = randomPitch ? Random.Range(SoundEffectManager.instance.randomPitchRange.x, SoundEffectManager.instance.randomPitchRange.y) : 1f;

        audioSource.loop = false;
        audioSource.Play();

        if (releaseCR != null) StopCoroutine(releaseCR);
        releaseCR = StartCoroutine(Co_ReleaseAfterTime());
    }
    
    public void PlayAudioWithPitch(AudioClip audioClip, float volume, float pitch)
    {
        audioSource.clip = audioClip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;
        audioSource.loop = false;
        audioSource.Play();

        if (releaseCR != null) StopCoroutine(releaseCR);
        releaseCR = StartCoroutine(Co_ReleaseAfterTime());
    }

    IEnumerator Co_ReleaseAfterTime()
    {
        yield return new WaitForSeconds(lifeTime);
        this.gameObject.Release();
    }
}
