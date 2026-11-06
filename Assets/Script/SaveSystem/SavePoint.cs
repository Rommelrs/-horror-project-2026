using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Interactable save point - like typewriters in Resident Evil
/// Player interacts to open save menu
/// </summary>
public class SavePoint : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "Press E to Save Game";
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("UI")]
    [SerializeField] private SaveMenuUI saveMenuUI;
    
    [Header("Subtitle")]
    [SerializeField] private SubtitleTrigger subtitleTrigger;
    
    [Header("Not While Questioners Are Here")]
    [Tooltip("Refuses to open while a living, awake Questioner is within this many metres. 0 = never refuses.")]
    [SerializeField] private float blockedByQuestionersWithin = 0f;
    [Tooltip("A sleeping (Fixed) one only counts this close: one stood beside the save point refuses it, one asleep a street away doesn't.")]
    [SerializeField] private float blockedBySleepingWithin = 12f;
    [Tooltip("Said instead of opening the save menu.")]
    [SerializeField] private UnityEngine.Localization.LocalizedString blockedLine;

    [Header("Stability Restoration")]
    [SerializeField] private bool restoreStability = true;
    [SerializeField] [Range(0f, 1f)] private float stabilityRestorePercent = 0.5f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip savePointActivateSound;
    
    [Header("Events")]
    public UnityEvent OnSavePointActivated;
    
    private bool playerInRange = false;
    private bool isInteracting = false;
    private bool subtitleAlreadyShown = false;
    
    private void Update()
    {
        // Check if player is in range
        CheckPlayerInRange();
        
        // Don't allow interaction if subtitle is active
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy())
            return;
        
        // Check for interaction input
        if (playerInRange && !isInteracting && Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("E pressed at save point!");
            if (QuestionersAreHere())
            {
                if (blockedLine != null && !blockedLine.IsEmpty)
                    SubtitleManager.ShowSubtitle(blockedLine, false);
                return;
            }
            ActivateSavePoint();
        }
    }
    
    // only looked up when E is pressed, so a horde costs nothing the rest of the time. Fixed ones are asleep.
    private bool QuestionersAreHere()
    {
        if (blockedByQuestionersWithin <= 0f) return false;
        foreach (var q in FindObjectsByType<QuestionerEnemy>(FindObjectsSortMode.None))
        {
            if (q.stats == null || q.health == null || q.health.IsDead) continue;
            float limit = q.stats.enemyType == EnemyType.Fixed ? blockedBySleepingWithin : blockedByQuestionersWithin;
            if ((q.transform.position - transform.position).sqrMagnitude <= limit * limit) return true;
        }
        return false;
    }

    private void CheckPlayerInRange()
    {
        // Check for Player instance directly (simpler approach)
        if (Player.instance != null)
        {
            float distance = Vector3.Distance(transform.position, Player.instance.transform.position);
            bool wasInRange = playerInRange;
            playerInRange = distance <= interactionRange;
            
            // Debug when player enters range
            if (playerInRange && !wasInRange)
            {
                Debug.Log($"Player entered save point range! Distance: {distance:F2}m");
            }
        }
        else
        {
            playerInRange = false;
        }
    }
    
    private void ActivateSavePoint()
    {
        isInteracting = true;
        
        // Play sound
        if (audioSource != null && savePointActivateSound != null)
        {
            audioSource.PlayOneShot(savePointActivateSound);
        }
        
        // Trigger event
        OnSavePointActivated?.Invoke();
        
        // If there's a subtitle trigger and it hasn't been shown yet, show it first
        if (subtitleTrigger != null && !subtitleAlreadyShown)
        {
            subtitleAlreadyShown = true;
            subtitleTrigger.TriggerSubtitle();
            StartCoroutine(WaitForSubtitleThenOpenMenu());
        }
        else
        {
            // No subtitle or already shown, open menu immediately
            OpenSaveMenu();
        }
    }
    
    private void OpenSaveMenu()
    {
        // Restore stability to player if enabled
        if (restoreStability && Player.instance != null)
        {
            PlayerStability playerStability = Player.instance.GetComponent<PlayerStability>();
            if (playerStability != null)
            {
                int stabilityToRestore = Mathf.RoundToInt(playerStability.maxStability * stabilityRestorePercent);
                playerStability.IncreaseStability(stabilityToRestore);
                Debug.Log($"Player stability restored by {stabilityToRestore} ({stabilityRestorePercent * 100}%)");
            }
        }
        
        // Open save menu UI
        if (saveMenuUI != null)
        {
            saveMenuUI.OpenSaveMenu();
        }
        else
        {
            Debug.LogWarning("SaveMenuUI not assigned to SavePoint!");
        }
        
        // Reset after a frame
        StartCoroutine(ResetInteraction());
    }
    
    private System.Collections.IEnumerator WaitForSubtitleThenOpenMenu()
    {
        // Wait until subtitle is no longer busy
        while (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy())
        {
            yield return null;
        }
        
        // Wait for cooldown period to finish
        while (SubtitleManager.instance != null && SubtitleManager.instance.IsWithinCooldownPeriod())
        {
            yield return null;
        }
        
        // Now open the save menu
        OpenSaveMenu();
    }
    
    private System.Collections.IEnumerator ResetInteraction()
    {
        yield return new WaitForEndOfFrame();
        isInteracting = false;
    }
    
    // Visualize interaction range in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
