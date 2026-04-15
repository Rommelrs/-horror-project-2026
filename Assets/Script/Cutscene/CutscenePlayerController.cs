using UnityEngine;
using UnityEngine.Playables;

public class CutscenePlayerController : MonoBehaviour
{
    [SerializeField] private PlayableDirector timeline;
    
    private void Start()
    {
        if (timeline != null)
        {
            timeline.played += OnTimelineStart;
            timeline.stopped += OnTimelineEnd;
        }
    }

    private void OnDestroy()
    {
        if (timeline != null)
        {
            timeline.played -= OnTimelineStart;
            timeline.stopped -= OnTimelineEnd;
        }
    }

    private void OnTimelineStart(PlayableDirector director)
    {
        FreezePlayer();
    }

    private void OnTimelineEnd(PlayableDirector director)
    {
        UnfreezePlayer();
    }

    public void FreezePlayer()
    {
        Debug.Log("FreezePlayer called!");
        if (Player.instance != null)
        {
            // Disable player movement script
            var playerMovement = Player.instance.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.enabled = false;
                Debug.Log("PlayerMovement disabled");
            }
            else
            {
                Debug.Log("PlayerMovement component not found!");
            }
            
            // Disable character controller
            if (Player.instance.controller != null)
            {
                Player.instance.controller.enabled = false;
                Debug.Log("CharacterController disabled");
            }
        }
        else
        {
            Debug.Log("Player.instance is null!");
        }
    }

    public void UnfreezePlayer()
    {
        Debug.Log("UnfreezePlayer called!");
        if (Player.instance != null)
        {
            // Re-enable character controller
            if (Player.instance.controller != null)
            {
                Player.instance.controller.enabled = true;
                Debug.Log("CharacterController enabled");
            }
            
            // Re-enable player movement script
            var playerMovement = Player.instance.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.enabled = true;
                Debug.Log("PlayerMovement enabled");
            }
        }
    }
}
