using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioTrigger : MonoBehaviour
{
    [SerializeField] AudioClip audioClip;
    [Tooltip("1 is the loudest an audio source can go. For a louder sound from further away, raise 'Full Volume Radius' instead.")]
    [SerializeField, Range(0f, 1f)] float volume = 1f;
    [SerializeField] bool destroyAfterCollision = false;
    [SerializeField] float delay = 0f;

    [Header("Proximity (3D)")]
    [Tooltip("Play the sound from a spot in the world, so it gets louder as the player gets closer and pans to its direction. Off = flat 2D sound.")]
    [SerializeField] bool playAtPosition = false;
    [Tooltip("Where the sound comes from. Leave empty to use this trigger's own position. Handy for a trigger at a doorway with the noise coming from round the corner.")]
    [SerializeField] Transform soundOrigin;
    [SerializeField] bool randomPitch = false;
    [Tooltip("Full volume within this many metres of the sound origin. Raise it to make the sound louder from further away (the default 1 starts fading almost immediately).")]
    [SerializeField, Min(0.1f)] float fullVolumeRadius = 1f;
    [Tooltip("The sound has faded out completely by this distance.")]
    [SerializeField, Min(1f)] float maxDistance = 50f;

    [Header("Linked triggers")]
    [Tooltip("Other triggers that stand in for this one (e.g. a second way into the same spot). When this one fires, they are removed - and recorded as used in the save, so they stay gone after loading. Set it on each of them pointing at the others.")]
    [SerializeField] AudioTrigger[] linkedTriggers;

    bool hasTriggered = false;
    bool retired = false;

    private void OnTriggerEnter(Collider other)
    {
        if (retired) return;
        if (other.gameObject.CompareTag("Player"))
        {
            // Check save system
            SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
            if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
            {
                hasTriggered = true;
                return;
            }

            TriggerAudio();
        }
    }

    public void TriggerAudio()
    {
        if (retired) return;

        // a linked trigger fired first this frame, or this is the one that fires: remove the others straight away
        if (linkedTriggers != null)
            foreach (AudioTrigger other in linkedTriggers)
                if (other != null && other != this) other.Retire();

        if (delay <= 0)
            PlayAudio();
        else
            StartCoroutine(Co_PlayAudioAfterDelay(delay));
    }

    void PlayAudio()
    {
        if (!hasTriggered)
        {
            hasTriggered = true;

            // Mark as triggered in save system
            SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
            if (saveableTrigger != null)
            {
                saveableTrigger.MarkAsTriggered();
            }
        }

        //Play Audio Clip
        if (playAtPosition)
            SoundEffectManager.instance.PlaySFXAtPosition(audioClip, (soundOrigin != null ? soundOrigin : transform).position, volume, randomPitch, fullVolumeRadius, maxDistance);
        else
            SoundEffectManager.instance.PlaySFX(audioClip, volume, randomPitch);

        if (destroyAfterCollision)
            Destroy(this.gameObject);
    }

    // Another trigger in the same group fired: this one is no longer needed. Remember that in the save, then remove it.
    void Retire()
    {
        if (retired) return;
        retired = true;

        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null)
            saveableTrigger.MarkAsTriggered();

        Destroy(gameObject);
    }

    IEnumerator Co_PlayAudioAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayAudio();
    }

    void OnDrawGizmosSelected()
    {
        if (!playAtPosition) return;
        Vector3 p = (soundOrigin != null ? soundOrigin : transform).position;
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f);
        Gizmos.DrawWireSphere(p, 0.3f);
        Gizmos.DrawLine(transform.position, p);
    }
}
