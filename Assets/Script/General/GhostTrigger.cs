using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostTrigger : MonoBehaviour
{
    [SerializeField] AudioClip ghostSound;
    
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        
        // Check save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
        {
            hasTriggered = true;
            return;
        }
        
        if (other.gameObject.CompareTag("Player"))
        {
            hasTriggered = true;
            
            // Mark as triggered in save system
            if (saveableTrigger != null)
            {
                saveableTrigger.MarkAsTriggered();
            }
            
            PlayerScared.instance.TriggerPlayerScaredBehaviour();

            if (ghostSound != null)
                AudioSource.PlayClipAtPoint(ghostSound, transform.position);

            Destroy(gameObject, 3f);
        }
    }
}
