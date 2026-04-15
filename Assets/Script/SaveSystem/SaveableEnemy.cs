using UnityEngine;

/// <summary>
/// Makes an enemy saveable - tracks if it died
/// Add this component to Enemy GameObjects
/// Also needs UniqueID component
/// </summary>
[RequireComponent(typeof(UniqueID))]
[RequireComponent(typeof(Enemy))]
public class SaveableEnemy : MonoBehaviour, ISaveable
{
    private UniqueID uniqueID;
    private Enemy enemy;
    
    private void Awake()
    {
        uniqueID = GetComponent<UniqueID>();
        enemy = GetComponent<Enemy>();
    }
    
    private void Start()
    {
        // Subscribe to enemy death
        if (enemy != null && enemy.health != null)
        {
            enemy.health.OnDeath.AddListener(OnEnemyDied);
        }
        else
        {
            Debug.LogWarning($"SaveableEnemy: Could not subscribe to death for {gameObject.name}. Enemy: {enemy != null}, Health: {enemy?.health != null}");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from death event
        if (enemy != null && enemy.health != null)
        {
            enemy.health.OnDeath.RemoveListener(OnEnemyDied);
        }
    }
    
    private void OnEnemyDied()
    {
        
        // Immediately register with SaveManager
        if (SaveManager.instance != null)
        {
            SaveManager.instance.RegisterDeadEnemy(uniqueID.ID);
        }
    }
    
    public void Save(SaveData saveData)
    {
        // Enemies are tracked by SaveManager runtime tracker - nothing to do here
    }
    
    public void Load(SaveData saveData)
    {
        
        // Check with SaveManager if this enemy was dead
        if (SaveManager.instance != null)
        {
            bool wasDead = SaveManager.instance.IsEnemyDead(uniqueID.ID);
            
            if (wasDead)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            Debug.LogWarning($"SaveManager.instance is null for enemy {gameObject.name}");
        }
    }
}
