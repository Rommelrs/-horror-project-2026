using UnityEngine;

/// <summary>
/// Ends a bag-throwing encounter for good: switches this spawner off once the player has walked past the given spot or holds the
/// given item, or once every listed enemy is dead. It only reads the world, so a loaded save lands in the right state by itself.
/// </summary>
public class ProjectileSpawnerStopper : MonoBehaviour
{
    [Header("Player has moved on")]
    [Tooltip("Stop as soon as the player holds this.")]
    [SerializeField] Item stopWhenHeld;
    [Tooltip("Stop as soon as the player comes within 'nearRadius' of this.")]
    [SerializeField] Transform stopWhenNear;
    [SerializeField, Min(0f)] float nearRadius = 10f;

    [Header("Encounter is over")]
    [Tooltip("Stop when all of these are dead. One that never got switched on doesn't count: it isn't in the fight.")]
    [SerializeField] Enemy[] enemies;

    void OnEnable()
    {
        if (ShouldStop()) gameObject.SetActive(false);
    }

    void Update()
    {
        if (ShouldStop()) gameObject.SetActive(false);
    }

    bool ShouldStop()
    {
        var player = Player.instance;
        if (player != null)
        {
            if (stopWhenHeld != null && player.inventory != null && player.inventory.HasItem(stopWhenHeld)) return true;
            if (stopWhenNear != null && (player.transform.position - stopWhenNear.position).sqrMagnitude <= nearRadius * nearRadius) return true;
        }
        return AllDead();
    }

    bool AllDead()
    {
        if (enemies == null || enemies.Length == 0) return false;
        foreach (var e in enemies)
            if (e != null && e.gameObject.activeInHierarchy && (e.health == null || !e.health.IsDead)) return false;   // a saved kill is destroyed on load: null counts as dead
        return true;
    }
}
