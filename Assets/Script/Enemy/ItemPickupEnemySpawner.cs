using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ItemPickupEnemySpawner : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private Item itemToWatch;

    [Header("Spawn Settings")]
    [SerializeField] private SpawnPoint[] spawnPoints;
    [SerializeField] private float delayBeforeSpawning = 0.5f;
    [SerializeField] private float delayBetweenSpawns = 1f;

    [Header("Continuous Spawning")]
    [SerializeField] private bool spawnContinuously = false;
    [SerializeField] private float delayBetweenWaves = 3f;
    [SerializeField] private int maxWaves = 0; // 0 = infinite

    [Header("Audio")]
    [SerializeField] private AudioSource musicAudioSource;
    [SerializeField] private AudioClip pickupMusic;
    
    [Header("Events")]
    [SerializeField] private UnityEvent onItemPickedUp;
    [SerializeField] private UnityEvent onSpawningStarted;
    [SerializeField] private UnityEvent onSpawningCompleted;
    [SerializeField] private UnityEvent onAllEnemiesDespawned;

    [System.Serializable]
    public class SpawnPoint
    {
        public GameObject enemyPrefab;
        public Transform spawnPosition;
        public int spawnCount = 1;
        public float delayBetweenEach = 0.5f;
    }

    private bool hasTriggered = false;
    private bool hadItemBefore = false;
    private List<GameObject> spawnedEnemies = new List<GameObject>();

    private void Start()
    {
        // If already triggered in a previous session (save/checkpoint), skip entirely
        SaveableInteractable saveable = GetComponent<SaveableInteractable>();
        if (saveable != null && saveable.WasAlreadyUsed())
        {
            hasTriggered = true;
            hadItemBefore = true; // prevent false detection on inventory restore
            return; // don't subscribe - sequence already happened
        }

        // Check if player already has the item
        if (itemToWatch != null && Player.instance != null)
        {
            hadItemBefore = PlayerHasItem(itemToWatch);
            Player.instance.inventory.OnInventoryItemUpdated.AddListener(OnInventoryUpdated);
        }
    }

    private void OnDestroy()
    {
        if (Player.instance != null && Player.instance.inventory != null)
        {
            Player.instance.inventory.OnInventoryItemUpdated.RemoveListener(OnInventoryUpdated);
        }
    }

    private void OnInventoryUpdated()
    {
        if (hasTriggered) return;

        bool hasItemNow = PlayerHasItem(itemToWatch);

        // Item was just picked up
        if (!hadItemBefore && hasItemNow)
        {
            hasTriggered = true;

            // Mark as used in save system so checkpoint restore doesn't re-trigger
            SaveableInteractable saveable = GetComponent<SaveableInteractable>();
            if (saveable != null) saveable.MarkAsUsed();

            onItemPickedUp?.Invoke();
            PlayPickupMusic();
            StartCoroutine(Co_SpawnEnemies());
        }

        hadItemBefore = hasItemNow;
    }

    private bool PlayerHasItem(Item item)
    {
        if (Player.instance == null || Player.instance.inventory == null) return false;

        foreach (var stack in Player.instance.inventory.GetItems())
        {
            if (stack.item == item) return true;
        }
        foreach (var stack in Player.instance.inventory.GetNotes())
        {
            if (stack.item == item) return true;
        }
        return false;
    }

    private IEnumerator Co_SpawnEnemies()
    {
        yield return new WaitForSeconds(delayBeforeSpawning);
        
        onSpawningStarted?.Invoke();

        int waveCount = 0;

        do
        {
            foreach (SpawnPoint spawnPoint in spawnPoints)
            {
                for (int i = 0; i < spawnPoint.spawnCount; i++)
                {
                if (spawnPoint.enemyPrefab != null && spawnPoint.spawnPosition != null && spawnPoint.spawnPosition.gameObject.activeInHierarchy)
                {
                    GameObject spawnedEnemy = Instantiate(spawnPoint.enemyPrefab, spawnPoint.spawnPosition.position, spawnPoint.spawnPosition.rotation);
                    spawnedEnemies.Add(spawnedEnemy);
                }

                    if (spawnPoint.spawnCount > 1)
                        yield return new WaitForSeconds(spawnPoint.delayBetweenEach);
                }

                yield return new WaitForSeconds(delayBetweenSpawns);
            }

            waveCount++;

            if (spawnContinuously)
                yield return new WaitForSeconds(delayBetweenWaves);

        } while (spawnContinuously && (maxWaves == 0 || waveCount < maxWaves));
        
        onSpawningCompleted?.Invoke();
    }

    public void StopSpawning()
    {
        spawnContinuously = false;
        StopAllCoroutines();
    }
    
    public void DespawnAllEnemies()
    {
        // Clean up null references first
        spawnedEnemies.RemoveAll(e => e == null);
        
        // Despawn all tracked enemies
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        
        spawnedEnemies.Clear();
        onAllEnemiesDespawned?.Invoke();
    }
    
    public void StopSpawningAndDespawn()
    {
        StopSpawning();
        DespawnAllEnemies();
    }
    
    public int GetSpawnedEnemyCount()
    {
        // Clean up null references
        spawnedEnemies.RemoveAll(e => e == null);
        return spawnedEnemies.Count;
    }

    private void PlayPickupMusic()
    {
        if (pickupMusic == null) return;

        if (musicAudioSource != null)
        {
            musicAudioSource.clip = pickupMusic;
            musicAudioSource.Play();
        }
        else
        {
            AudioSource.PlayClipAtPoint(pickupMusic, Player.instance.transform.position);
        }
    }

    public void StopMusic()
    {
        if (musicAudioSource != null)
            musicAudioSource.Stop();
    }
}
