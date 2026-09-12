using UnityEngine;

/// <summary>
/// Place this on any GameObject to trigger a checkpoint.
/// Can be activated via:
///   - Player walking into a trigger collider (set triggerOnPlayerEnter = true)
///   - Calling Trigger() from a UnityEvent (e.g. after cutscene, item pickup, etc.)
/// </summary>
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("Name shown in logs / debug. Helps identify which checkpoint fired.")]
    [SerializeField] string checkpointName = "Checkpoint";

    [Tooltip("Only trigger once. Subsequent calls are ignored.")]
    [SerializeField] bool triggerOnce = true;

    [Tooltip("Activate checkpoint when player walks into this trigger collider.")]
    [SerializeField] bool triggerOnPlayerEnter = false;

    bool hasTriggered = false;

    /// <summary>Call this from any UnityEvent to save a checkpoint.</summary>
    public void Trigger()
    {
        if (triggerOnce && hasTriggered) return;
        hasTriggered = true;

        if (CheckpointManager.instance != null)
            CheckpointManager.instance.TriggerCheckpoint(checkpointName);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnPlayerEnter) return;
        if (!other.CompareTag("Player")) return;
        Trigger();
    }
}
