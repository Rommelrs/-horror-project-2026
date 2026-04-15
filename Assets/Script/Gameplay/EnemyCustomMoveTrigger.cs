using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCustomMoveTrigger : MonoBehaviour
{
    [SerializeField] EnemyAddonCustomMoveToSpot enemyAddon;
    [SerializeField] bool triggerOnce = true;

    bool hasTriggered = false;

    SaveableTrigger saveableTrigger;
    
    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered)
            return;
        
        // Check save system
        saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
        {
            hasTriggered = true;
            return;
        }

        if (other.CompareTag("Player"))
        {
            if (enemyAddon != null)
            {
                enemyAddon.OnPlayerDetected();
                hasTriggered = true;
                
                // Mark as triggered in save system
                if (saveableTrigger != null)
                {
                    saveableTrigger.MarkAsTriggered();
                }
            }
            else
            {
                Debug.LogWarning("EnemyAddonCustomMoveToSpot not assigned to trigger!");
            }
        }
    }
}
