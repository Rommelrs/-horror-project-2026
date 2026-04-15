using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class OnPlayerTrigger : MonoBehaviour
{
    [SerializeField] bool destroyAfterTrigger = false;
    [SerializeField] float delay = 0f;

    [Header("Item Requirement")]
    [SerializeField] bool requireItem = false;
    [SerializeField] Item requiredItem;

    [Header("Activate Enemies")]
    [SerializeField] bool activateEnemies = false;
    [SerializeField] List<GameObject> enemiesToActivate = new List<GameObject>();

    [Header("Sound")]
    [SerializeField] AudioClip triggerSound;
    [SerializeField] float soundVolume = 1f;
    [SerializeField] bool use3DSound = false;

    [Space(5)]
    [SerializeField] UnityEvent onTrigger;

    bool isTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            // Only check save system if this specific script hasn't been triggered yet
            if (!isTriggered)
            {
                SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
                if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
                {
                    isTriggered = true; // Mark as triggered to prevent re-trigger
                    return;
                }
            }
            
            Trigger();
        }
    }

    public void Trigger()
    {
        if (isTriggered)
            return;
        
        // Check if item is required and player has it
        if (requireItem)
        {
            if (!PlayerHasRequiredItem())
                return;
        }
        
        isTriggered = true;
        
        // Don't mark save system here - let EnemyAggroTrigger handle it if it exists
        // This prevents conflicts when both scripts are on the same GameObject
        if (GetComponent<EnemyAggroTrigger>() == null)
        {
            SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
            if (saveableTrigger != null)
            {
                saveableTrigger.MarkAsTriggered();
            }
        }

        //Play Sound
        if (triggerSound != null)
        {
            PlayTriggerSound();
        }

        //Activate Enemies
        if (activateEnemies)
        {
            ActivateEnemies();
        }

        //Call Event
        onTrigger?.Invoke();

        if (destroyAfterTrigger)
        {
            if (delay <= 0)
                Destroy(this.gameObject);
            else
                StartCoroutine(Co_DestoryAfterDelay());
        }
    }

    IEnumerator Co_DestoryAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        Destroy(this.gameObject);
    }

    void ActivateEnemies()
    {
        foreach (var enemy in enemiesToActivate)
        {
            if (enemy != null)
                enemy.SetActive(true);
        }
    }

    bool PlayerHasRequiredItem()
    {
        if (requiredItem == null || Player.instance == null || Player.instance.inventory == null)
            return false;

        foreach (var stack in Player.instance.inventory.GetItems())
        {
            if (stack.item == requiredItem)
                return true;
        }

        foreach (var stack in Player.instance.inventory.GetNotes())
        {
            if (stack.item == requiredItem)
                return true;
        }

        return false;
    }

    void PlayTriggerSound()
    {
        if (use3DSound)
        {
            // Play 3D sound at trigger position
            AudioSource.PlayClipAtPoint(triggerSound, transform.position, soundVolume);
        }
        else
        {
            // Play 2D sound through SoundEffectManager if available
            if (SoundEffectManager.instance != null)
            {
                SoundEffectManager.instance.PlaySFX(triggerSound, soundVolume);
            }
            else
            {
                // Fallback: play at camera position
                AudioSource.PlayClipAtPoint(triggerSound, Camera.main.transform.position, soundVolume);
            }
        }
    }
}
