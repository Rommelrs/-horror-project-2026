using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class ItemDropEntry
{
    public GameObject itemPrefab;
    [Range(0, 100)] public float dropChance = 100f; // Percentage chance to drop
}

public class EnemyItemDrop : MonoBehaviour
{
    [SerializeField] private ItemDropEntry[] itemDrops; // List of items with their drop chances
    [SerializeField] private Vector3 dropOffset = new Vector3(0, 0.5f, 0);
    [Tooltip("Drops land on the floor under the drop point (the model's bottom on the ground) instead of hanging where they spawn.")]
    [SerializeField] private bool snapToGround = true;

    [Header("Low-health extra drop")]
    [Tooltip("Dropped as well (guaranteed) when, at the moment this enemy dies, the player's health is below the fraction " +
             "below AND he carries none of the 'only if out of' items. E.g. the duct tape Questioner gives a Med Kit to a hurt " +
             "player with no heals left.")]
    [SerializeField] private GameObject lowHealthDrop;
    [FormerlySerializedAs("lowHealthAtOrBelow")]
    [SerializeField, Range(0f, 1f)] private float lowHealthBelow = 0.4f;
    [Tooltip("The extra drop only comes if the player has none of these (e.g. Med Kit, Pain Killer).")]
    [SerializeField] private Item[] onlyIfOutOf;
    [Tooltip("Where the extra drop lands, from the enemy (beside the main drop, not inside it).")]
    [SerializeField] private Vector3 lowHealthDropOffset = new Vector3(0.6f, 0.5f, 0f);

    private Enemy enemy;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    private void Start()
    {
        if (enemy != null)
        {
            enemy.OnEnemyDied.AddListener(DropItem);
        }
    }

    private void OnDestroy()
    {
        if (enemy != null)
        {
            enemy.OnEnemyDied.RemoveListener(DropItem);
        }
    }

    private void DropItem()
    {
        DropLowHealthItem();

        if (itemDrops == null || itemDrops.Length == 0)
            return;
        
        // Try each item drop
        foreach (ItemDropEntry entry in itemDrops)
        {
            if (entry.itemPrefab == null)
                continue;
            
            // Roll for drop chance
            float roll = Random.Range(0f, 100f);
            if (roll <= entry.dropChance)
            {
                Spawn(entry.itemPrefab, transform.position + dropOffset);
                return; // Only drop one item
            }
        }
    }

    private void DropLowHealthItem()
    {
        if (lowHealthDrop == null || Player.instance == null || Player.instance.health == null) return;
        var h = Player.instance.health;
        // whole hit points: a float ratio compared as 0.4 (double) < 0.4f (0.40000000596) let exactly 40/100 count as below
        int below = Mathf.RoundToInt(h.GetMaxHealthValue() * lowHealthBelow);
        if (h.GetHealthValue() >= below) return;
        var inventory = Player.instance.inventory;
        if (onlyIfOutOf != null && inventory != null)
            foreach (var item in onlyIfOutOf)
                if (item != null && inventory.CountOf(item) > 0) return;
        Spawn(lowHealthDrop, transform.position + transform.rotation * lowHealthDropOffset);
    }

    void Spawn(GameObject prefab, Vector3 position)
    {
        var drop = Instantiate(prefab, position, Quaternion.identity);
        if (snapToGround) SnapToGround(drop);
    }

    /// <summary>Puts the drop's visible model on the floor below it (the pickup prefabs keep their models 0.1-0.5 m above
    /// their root, plus the drop offset: a Med Kit hung in the air).</summary>
    void SnapToGround(GameObject drop)
    {
        Vector3 from = drop.transform.position + Vector3.up * 1f;
        var hits = Physics.RaycastAll(from, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 ground = Vector3.zero;
        foreach (var hit in hits)
        {
            // not the corpse it fell from, not the drop itself, not the player
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(drop.transform) || hit.collider.CompareTag("Player")) continue;
            if (hit.distance < best) { best = hit.distance; ground = hit.point; }
        }
        if (best == float.MaxValue) return;

        bool any = false;
        Bounds b = default;
        foreach (var r in drop.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        float lift = any ? drop.transform.position.y - b.min.y : 0f;
        var pos = drop.transform.position;
        pos.y = ground.y + lift + 0.01f;
        drop.transform.position = pos;
    }
}
