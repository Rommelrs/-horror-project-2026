using UnityEngine;

// Shows the "Press E to interact" hint whenever the player is near this interactable, and hides
// it when they walk away. If the player picks the item up instead, the prompt fades out shortly
// after (instead of instantly) so the pickup feedback has a moment to land first.
public class InteractPromptTrigger : MonoBehaviour
{
    [Tooltip("Delay before the prompt fades out after this object is picked up (destroyed).")]
    [SerializeField] float hideDelayOnPickup = 1f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (InteractPromptUI.instance == null) return;

        InteractPromptUI.instance.ShowPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (InteractPromptUI.instance == null) return;

        InteractPromptUI.instance.HidePrompt();
    }

    private void OnDestroy()
    {
        if (InteractPromptUI.instance != null)
            InteractPromptUI.instance.HidePrompt(hideDelayOnPickup);
    }
}
