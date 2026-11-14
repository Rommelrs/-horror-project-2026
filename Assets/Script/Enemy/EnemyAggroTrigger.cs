using UnityEngine;

public class EnemyAggroTrigger : MonoBehaviour
{
    [SerializeField] private Enemy[] enemies;
    [SerializeField] private bool destroyTriggerAfterActivation = true;

    private bool hasTriggered = false;

    // Loaded from a save / checkpoint taken after the player woke them: the enemies must be awake again (the Runner between
    // Alley 2 and 3 came back in his fixed, idle type and never chased). Waits a frame so SaveableEnemy has removed the dead.
    private System.Collections.IEnumerator Start()
    {
        yield return null;
        if (hasTriggered) yield break;
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger == null || !saveableTrigger.WasAlreadyTriggered()) yield break;
        hasTriggered = true;
        Debug.Log("[AggroTrigger] fired before this save: waking its enemies again");
        WakeEnemies();
        if (destroyTriggerAfterActivation)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only care about the player
        if (!other.CompareTag("Player")) return;

        Debug.Log($"[AggroTrigger] Player entered | hasTriggered: {hasTriggered}");

        Activate();
    }

    /// <summary>Wakes the enemies, once. Also callable from a UnityEvent, so a sequence can't miss it if the player never crosses the box.</summary>
    public void Activate()
    {
        if (hasTriggered) return;

        // Check save system
        SaveableTrigger saveableTrigger = GetComponent<SaveableTrigger>();
        if (saveableTrigger != null && saveableTrigger.WasAlreadyTriggered())
        {
            Debug.Log("[AggroTrigger] Already triggered in save system, skipping");
            hasTriggered = true;
            return;
        }

        hasTriggered = true;

        // Mark as triggered in save system
        if (saveableTrigger != null)
        {
            saveableTrigger.MarkAsTriggered();
        }

        WakeEnemies();

        if (destroyTriggerAfterActivation)
            Destroy(gameObject);
    }

    private void WakeEnemies()
    {
        // Activate all enemies
        if (enemies != null && enemies.Length > 0)
        {
            foreach (Enemy enemy in enemies)
            {
                if (enemy != null)
                {
                    // switched off on purpose by the story (e.g. the Alley 3 bag bearer gone at the knockdown door): stays gone
                    var hidden = enemy.GetComponent<SaveableDisableState>();
                    if (!enemy.gameObject.activeSelf && hidden != null && hidden.WasSwitchedOff) continue;

                    if (!enemy.gameObject.activeInHierarchy)
                    {
                        Debug.Log($"[AggroTrigger] Activating inactive enemy: {enemy.name}");
                        enemy.gameObject.SetActive(true);
                    }

                    Debug.Log($"[AggroTrigger] Setting {enemy.name} to Aggressive + Chase");

                    // Use ChangeEnemyType for all enemies (handles state transitions properly)
                    enemy.ChangeEnemyType(EnemyType.Aggressive);
                }
                else
                {
                    Debug.LogWarning("[AggroTrigger] Enemy reference is NULL in array!");
                }
            }
        }
    }
}
