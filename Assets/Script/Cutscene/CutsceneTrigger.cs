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
    
    private static bool hasPlayed = false;
    private Player originalPlayer; // Store the player we disabled at the start
    
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
        
        // Store original player and disable movement
        originalPlayer = Player.instance; // Remember which player we're disabling
        
        if (originalPlayer != null)
        {
            var movement = originalPlayer.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.enabled = false;
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
        
        // Re-enable movement on the ORIGINAL player we disabled
        if (originalPlayer != null && originalPlayer.gameObject.activeInHierarchy)
        {
            var movement = originalPlayer.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.enabled = true;
        }
        // Fallback to current Player.instance if original is gone
        else if (Player.instance != null)
        {
            var movement = Player.instance.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.enabled = true;
        }
        
        // Disable the entire cutscene GameObject
        gameObject.SetActive(false);
    }
}
