using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

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
    
    // Per trigger, by object name (static so it survives the reload after dying). It used to be one flag shared by every trigger:
    // the car crash set it, then the after-store cutscene thought it had already played and never froze the player.
    private static readonly HashSet<string> played = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => played.Clear();

    /// <summary>New game: every cutscene may play again.</summary>
    public static void ResetPlayed() => played.Clear();

    private Player originalPlayer; // Store the player we disabled at the start
    
    // Store component states to restore later
    private bool wasMovementEnabled;
    private bool wasInputEnabled;
    private bool holdingPipe;
    
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
            played.Add(gameObject.name);
            return; // Already played in save
        }
        
        // Check if we should play the cutscene
        if (playOnlyOnce && played.Contains(gameObject.name))
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
                played.Add(gameObject.name);
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
        
        // With only the pipe he stands in its carry pose (his own Animator Controller, so no animation track for him on the timeline).
        // It reads its input actions itself, so disabling PlayerInput above wouldn't stop him raising it mid cutscene.
        holdingPipe = originalPlayer != null && PlayerMeleeWeapon.OwnsPipe && !originalPlayer.inventory.HasWeapon();
        if (holdingPipe) PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.Carry);

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

        if (holdingPipe) PlayerMeleeWeapon.SetCutscene(PlayerMeleeWeapon.CutsceneHold.None);
        
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
