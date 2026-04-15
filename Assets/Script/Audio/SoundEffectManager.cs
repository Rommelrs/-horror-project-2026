using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ToolBox.Pools;

public class SoundEffectManager : MonoBehaviour
{
    public static SoundEffectManager instance;

    [Header("Default Settings")]
    [SerializeField] AudioPoint audioPointPrefab;
    [Range(0f, 1f)] public float masterVolume = 1f;
    public Vector2 randomPitchRange = new Vector2(0.95f, 1.05f);

    AudioSource audioSource;

    private void Awake()
    {
        // Singleton
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
    }

    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f, bool randomPitch = false)
    {
        if (clip == null)
            return;

        AudioPoint audioPoint = audioPointPrefab.gameObject.Reuse(position, Quaternion.identity).GetComponent<AudioPoint>();
        audioPoint.lifeTime = clip.length + 0.4f;
        audioPoint.PlayAudio(clip, volume * masterVolume, randomPitch);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f, bool randomPitch = false)
    {
        if (clip == null)
            return;

        audioSource.pitch = randomPitch
            ? Random.Range(randomPitchRange.x, randomPitchRange.y)
            : 1f;

        audioSource.PlayOneShot(clip, volume * masterVolume);
    }
    
    public void PlaySFXAtPositionWithPitch(AudioClip clip, Vector3 position, float pitch, float volume = 1f)
    {
        if (clip == null)
            return;

        AudioPoint audioPoint = audioPointPrefab.gameObject.Reuse(position, Quaternion.identity).GetComponent<AudioPoint>();
        audioPoint.lifeTime = clip.length + 0.4f;
        // Don't multiply by masterVolume to allow louder sounds
        audioPoint.PlayAudioWithPitch(clip, volume, pitch);
    }
}
