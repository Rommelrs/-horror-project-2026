using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioTrigger : MonoBehaviour
{
    [SerializeField] AudioClip audioClip;
    [SerializeField] bool destroyAfterCollision = false;
    [SerializeField] float delay = 0f;

    bool hasTriggered = false;
    
    private void OnTriggerEnter(Collider other)
    {
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
        SoundEffectManager.instance.PlaySFX(audioClip);

        if (destroyAfterCollision)
            Destroy(this.gameObject);
    }

    IEnumerator Co_PlayAudioAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayAudio();
    }
}
