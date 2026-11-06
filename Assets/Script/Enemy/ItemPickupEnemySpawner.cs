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
    [Tooltip("At most this many enemies from this spawner alive at once (the line-up counts): it waits at the limit and carries "
           + "on as they die. Hundreds at once drop the frame rate hard. 0 = no limit.")]
    [SerializeField] private int maxAliveEnemies = 0;

    [Header("Other Enemies")]
    [Tooltip("While the sequence runs, any other enemy (not spawned by this spawner) within this distance (m) of a spawned "
           + "enemy or a spawn point is removed, so nothing else (a BagBearer, a normal Questioner...) interrupts it. 0 = off.")]
    [SerializeField] private float clearOtherEnemiesRadius = 20f;

    [Header("Chorus Line-Up")]
    [Tooltip("Spawned the moment the sequence starts, standing still at Line Up Centre (put it out of the player's sight, on "
           + "their way), until they all scream at once as the player reaches them. Its forward = the way they face.")]
    [SerializeField] private GameObject lineUpPrefab;
    [SerializeField] private Transform lineUpCentre;
    [SerializeField] private int lineUpCount = 16;
    [SerializeField] private int lineUpColumns = 4;
    [SerializeField] private float lineUpWidth = 5.5f;
    [SerializeField] private float lineUpDepth = 4.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource musicAudioSource;
    [SerializeField] private AudioClip pickupMusic;
    [SerializeField] private AudioClip pickupSFX;
    
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

        // Fire onItemPickedUp after the delay
        onItemPickedUp?.Invoke();
        PlayPickupSFX();
        PlayPickupMusic();
        
        onSpawningStarted?.Invoke();
        QuestionerEnemy.ResetHordeChorus();
        SpawnLineUp();
        if (clearOtherEnemiesRadius > 0f) StartCoroutine(Co_ClearOtherEnemies());

        int waveCount = 0;

        do
        {
            foreach (SpawnPoint spawnPoint in spawnPoints)
            {
                for (int i = 0; i < spawnPoint.spawnCount; i++)
                {
                if (spawnPoint.enemyPrefab != null && spawnPoint.spawnPosition != null && spawnPoint.spawnPosition.gameObject.activeInHierarchy)
                {
                    while (AtEnemyLimit()) yield return new WaitForSeconds(0.25f);

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

    private bool AtEnemyLimit()
    {
        if (maxAliveEnemies <= 0) return false;
        spawnedEnemies.RemoveAll(e => e == null);
        int alive = 0;
        foreach (var e in spawnedEnemies)
        {
            var enemy = e.GetComponent<Enemy>();
            if (e.activeInHierarchy && (enemy == null || enemy.health == null || !enemy.health.IsDead)) alive++;
        }
        return alive >= maxAliveEnemies;
    }

    private void SpawnLineUp()
    {
        if (lineUpPrefab == null || lineUpCentre == null) return;
        int rows = Mathf.CeilToInt(lineUpCount / (float)lineUpColumns);
        for (int i = 0; i < lineUpCount; i++)
        {
            int row = i / lineUpColumns, column = i % lineUpColumns;
            // rows staggered, so the back ones show between the front ones
            float x = ((column + (row % 2 == 0 ? 0.25f : 0.75f)) / lineUpColumns - 0.5f) * lineUpWidth;
            float z = (rows > 1 ? 0.5f - row / (rows - 1f) : 0f) * lineUpDepth;
            Vector3 at = lineUpCentre.TransformPoint(new Vector3(x + Random.Range(-0.15f, 0.15f), 0f, z + Random.Range(-0.15f, 0.15f)));
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            GameObject spawned = Instantiate(lineUpPrefab, at, lineUpCentre.rotation);
            spawnedEnemies.Add(spawned);
            var questioner = spawned.GetComponent<QuestionerEnemy>();
            if (questioner != null) questioner.JoinLineUp();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (lineUpCentre == null) return;
        Gizmos.color = Color.red;
        Gizmos.matrix = lineUpCentre.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up, new Vector3(lineUpWidth, 2f, lineUpDepth));
    }

    /// <summary>Every half second: other enemies near the horde or its spawn points disappear.</summary>
    private IEnumerator Co_ClearOtherEnemies()
    {
        var wait = new WaitForSeconds(0.5f);
        var mine = new HashSet<GameObject>();
        var centres = new List<Vector3>();
        float r2 = clearOtherEnemiesRadius * clearOtherEnemiesRadius;
        while (true)
        {
            mine.Clear();
            centres.Clear();
            foreach (var e in spawnedEnemies) if (e != null) { mine.Add(e); centres.Add(e.transform.position); }
            foreach (var sp in spawnPoints) if (sp.spawnPosition != null) centres.Add(sp.spawnPosition.position);

            foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if (mine.Contains(enemy.gameObject) || enemy.health == null || enemy.health.IsDead) continue;
                if (enemy is QuestionerEnemy q && q.hordeMode) continue;
                Vector3 at = enemy.transform.position;
                foreach (var c in centres)
                {
                    if ((at - c).sqrMagnitude > r2) continue;
                    enemy.gameObject.SetActive(false);
                    break;
                }
            }
            yield return wait;
        }
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

    private void PlayPickupSFX()
    {
        if (pickupSFX == null) return;

        if (musicAudioSource != null)
            musicAudioSource.PlayOneShot(pickupSFX);
        else if (SoundEffectManager.instance != null)
            SoundEffectManager.instance.PlaySFXAtPosition(pickupSFX, Player.instance.transform.position);
        else
            AudioSource.PlayClipAtPoint(pickupSFX, Player.instance.transform.position);
    }

    private void PlayPickupMusic()
    {
        if (pickupMusic == null) return;

        if (musicAudioSource != null)
        {
            musicAudioSource.clip = pickupMusic;
            musicAudioSource.Play();
        }
        else if (SoundEffectManager.instance != null)
        {
            SoundEffectManager.instance.PlaySFXAtPosition(pickupMusic, Player.instance.transform.position);
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
