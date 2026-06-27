using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;

public class CutsceneTrigger : MonoBehaviour
{
    [SerializeField] private PlayableDirector timeline;
    [SerializeField] private string previousSceneName = "Store"; // Name of the store scene
    [SerializeField] private bool playOnlyOnce = true;
    
    [Header("End Settings")]
    [SerializeField] private float delayBeforeDisabling = 1f; // Time to let audio/effects finish
    [SerializeField] private UnityEvent onCutsceneComplete; // Events to trigger before disabling
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private static bool hasPlayed = false;
    private Player originalPlayer; // Store the player we disabled at the start
    
    // Store component states to restore later
    private bool wasMovementEnabled;
    private bool wasInputEnabled;
    
    private void Start()
    {
        if (timeline != null)
        {
            timeline.stopped += OnCutsceneEnd;
        }
        
        // Check save system first
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
        {
            hasPlayed = true;
            return; // Already played in save
        }
        
        // Check if we should play the cutscene
        if (playOnlyOnce && hasPlayed)
        {
            // Already played, don't play again
            return;
        }
        
        // Check if we came from the store scene
        if (PlayerPrefs.HasKey("PreviousScene"))
        {
            string lastScene = PlayerPrefs.GetString("PreviousScene");
            if (lastScene == previousSceneName)
            {
                PlayCutscene();
                hasPlayed = true;
            }
        }
    }
    
    public void PlayCutscene()
    {
        // Mark as triggered in save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null)
        {
            saveableTrigger.MarkAsTriggered();
        }
        
        // Store original player and disable all input/control components
        originalPlayer = Player.instance; // Remember which player we're disabling
        
        if (originalPlayer != null)
        {
            // Disable movement
            var movement = originalPlayer.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                wasMovementEnabled = movement.enabled;
                movement.enabled = false;
            }
            
            // Disable input (prevents any input from working)
            var playerInput = originalPlayer.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                wasInputEnabled = playerInput.enabled;
                playerInput.enabled = false;
            }
            
            // Disable interaction
            var doorInteraction = originalPlayer.GetComponent<DoorInteractionHandler>();
            if (doorInteraction != null)
                doorInteraction.enabled = false;
        }
        
        // Play timeline
        if (timeline != null)
            timeline.Play();
    }
    
    private void OnCutsceneEnd(PlayableDirector director)
    {
        StartCoroutine(Co_EndCutscene());
    }
    
    private IEnumerator Co_EndCutscene()
    {
        // Trigger any events (like fading out audio, activating objects, etc.)
        onCutsceneComplete?.Invoke();
        
        // Wait for delay to let things finish smoothly
        yield return new WaitForSeconds(delayBeforeDisabling);
        
        // Re-enable all components on the ORIGINAL player we disabled
        if (originalPlayer != null && originalPlayer.gameObject.activeInHierarchy)
        {
            // Re-enable movement
            var movement = originalPlayer.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.enabled = wasMovementEnabled;
            
            // Re-enable input
            var playerInput = originalPlayer.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = wasInputEnabled;
            
            // Re-enable interaction
            var doorInteraction = originalPlayer.GetComponent<DoorInteractionHandler>();
            if (doorInteraction != null)
                doorInteraction.enabled = true;
            
            // DEBUG: Check player state
            if (enableDebugLogs)
            {
                CharacterController cc = originalPlayer.GetComponent<CharacterController>();
                Collider col = originalPlayer.GetComponent<Collider>();
                Debug.Log($"[CutsceneTrigger] Player state after cutscene:");
                Debug.Log($"  - Name: {originalPlayer.name}");
                Debug.Log($"  - Active: {originalPlayer.gameObject.activeSelf}");
                Debug.Log($"  - Layer: {LayerMask.LayerToName(originalPlayer.gameObject.layer)}");
                Debug.Log($"  - Tag: {originalPlayer.tag}");
                Debug.Log($"  - Position: {originalPlayer.transform.position}");
                Debug.Log($"  - CharacterController enabled: {(cc != null ? cc.enabled.ToString() : "NULL")}");
                Debug.Log($"  - Collider enabled: {(col != null ? col.enabled.ToString() : "NULL")}");
                Debug.Log($"  - PlayerMovement enabled: {movement.enabled}");
                Debug.Log($"  - Time.timeScale: {Time.timeScale}");
                Debug.Log($"  - GameManager.IsPaused: {GameManager.IsPaused}");
                
                // List all components
                var allComponents = originalPlayer.GetComponents<MonoBehaviour>();
                Debug.Log($"  - All MonoBehaviour components:");
                foreach (var comp in allComponents)
                {
                    Debug.Log($"    * {comp.GetType().Name}: enabled={comp.enabled}");
                }
            }
        }
        // Fallback to current Player.instance if original is gone
        else if (Player.instance != null)
        {
            var movement = Player.instance.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.enabled = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[CutsceneTrigger] Using fallback Player.instance: {Player.instance.name}");
            }
        }
        
        // Disable the entire cutscene GameObject
        gameObject.SetActive(false);
    }
}
