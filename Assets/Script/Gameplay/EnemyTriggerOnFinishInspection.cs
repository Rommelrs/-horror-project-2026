using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemyTriggerOnFinishInspection : MonoBehaviour
{
    [SerializeField] string inspectionName;
    //[SerializeField] TimelineEnemyController timelineEnemyController;
    public UnityEvent onFinishInspection;
    [SerializeField] float delay = 2f;
    
    private bool hasTriggered = false;

    private void Start()
    {
        // Check save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
        {
            hasTriggered = true;
            return;
        }
        
        if (ItemInspectionHandler.instance)
            ItemInspectionHandler.instance.onCloseInspection += OnCloseInspection;
    }

    private void OnDestroy()
    {
        if (ItemInspectionHandler.instance)
            ItemInspectionHandler.instance.onCloseInspection -= OnCloseInspection;
    }

    void OnCloseInspection(string itemName)
    {
        if (hasTriggered) return;
        
        if(itemName == inspectionName)
        {
            hasTriggered = true;
            
            // Mark as triggered in save system
            SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
            if (saveableTrigger != null)
            {
                saveableTrigger.MarkAsTriggered();
            }
            
            //If inspected Item Name match trigger Enemy Timeline
            StartCoroutine(Co_PlayTimelineAfterDelay());
        }
    }

    IEnumerator Co_PlayTimelineAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delay);

        onFinishInspection?.Invoke();

        //timelineEnemyController.PlayTimeline();
    }
}
