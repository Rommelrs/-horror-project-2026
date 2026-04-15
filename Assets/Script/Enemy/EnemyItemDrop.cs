using UnityEngine;

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
                Instantiate(entry.itemPrefab, transform.position + dropOffset, Quaternion.identity);
                return; // Only drop one item
            }
        }
    }
}
