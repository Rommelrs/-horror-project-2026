using UnityEngine;

// Shows "Press I to open inventory" while the player is near the fusebox, already has the fuse,
// hasn't powered it yet, and isn't already looking at the inventory menu. Continuously re-checks
// all of those conditions every frame instead of only reacting to trigger enter/exit, so it
// correctly hides the moment the player opens the inventory, reappears if they close it again
// without using the fuse, and hides for good once the fuse is actually used.
public class FuseboxInventoryPrompt : MonoBehaviour
{
    Fusebox fusebox;
    bool playerInRange;
    bool isShowing;

    private void Awake()
    {
        fusebox = GetComponent<Fusebox>();
    }

    private void Update()
    {
        bool shouldShow = playerInRange
            && !fusebox.hasEnergy
            && Player.instance != null && Player.instance.inventory.HasFuse()
            && (InventoryUI.instance == null || !InventoryUI.instance.InventoryIsActive());

        if (shouldShow == isShowing) return;
        if (InteractPromptUI.instance == null) return;

        isShowing = shouldShow;
        if (isShowing)
            InteractPromptUI.instance.ShowPrompt();
        else
            InteractPromptUI.instance.HidePrompt();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }
}
