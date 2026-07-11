using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DangerZone : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float timeUntilTrigger = 5f;

    [Header("Damage Settings")]
    [SerializeField] private int weakpointDamage = 100;
    [SerializeField] private float hitstopDuration = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioSource weakpointAudioSource;
    [SerializeField] private AudioClip[] weakpointHitSounds;

    [Header("Camera Shake")]
    [SerializeField] private CameraShaker cameraShaker;
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float shakeDuration = 0.2f;

    [Header("Post Weakpoint Hit Sound")]
    [SerializeField] private AudioSource postHitAudioSource;
    [SerializeField] private AudioClip postHitSound;
    [SerializeField] private float postHitSoundDelay = 1f;

    [Header("Activate On Trigger")]
    [SerializeField] private GameObject[] objectsToActivate;
    [SerializeField] private AudioClip activateSound;
    [SerializeField] private float activateSoundDelay = 0f;

    [Header("Deactivate On Trigger")]
    [SerializeField] private GameObject[] objectsToDeactivate;

    [Header("Stop Spawners On Trigger")]
    [SerializeField] private ItemPickupEnemySpawner[] spawnersToStop;
    [SerializeField] private bool despawnEnemiesFromSpawners = true;

    [Header("Disable Spawn Points")]
    [SerializeField] private GameObject[] spawnPointsToDisable;

    [Header("Optional")]
    [SerializeField] private bool destroyAfterTrigger = false;
    [SerializeField] private bool resetTimerOnExit = true;
    [SerializeField] private bool destroyWeakpointHitbox = true;
    [SerializeField] private bool allowRetrigger = true;
    [Tooltip("Time to wait after triggering before the zone can trigger again")]
    [SerializeField] private float retriggerCooldown = 1f;

    private float playerTimeInZone = 0f;
    private bool playerInZone = false;
    private bool hasTriggered = false;
    private List<Enemy> enemiesInZone = new List<Enemy>();
    private float nextTriggerTime = 0f;

    private void Update()
    {
        // Skip if one-time trigger and already triggered
        if (!allowRetrigger && hasTriggered) return;
        
        // Skip if on cooldown
        if (Time.time < nextTriggerTime) return;

        if (playerInZone)
        {
            playerTimeInZone += Time.deltaTime;

            if (playerTimeInZone >= timeUntilTrigger)
            {
                TriggerWeakpointHit();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = true;
            Debug.Log("DangerZone: Player entered");
        }

        // Track enemies
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null)
            enemy = other.GetComponentInParent<Enemy>();

        if (enemy != null && !enemiesInZone.Contains(enemy))
        {
            enemiesInZone.Add(enemy);
            Debug.Log("DangerZone: Enemy entered - " + enemy.name);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;
            Debug.Log("DangerZone: Player exited");

            if (resetTimerOnExit)
                playerTimeInZone = 0f;
        }

        // Remove enemies
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null)
            enemy = other.GetComponentInParent<Enemy>();

        if (enemy != null)
        {
            enemiesInZone.Remove(enemy);
        }
    }

    private void TriggerWeakpointHit()
    {
        hasTriggered = true;
        playerTimeInZone = 0f; // Reset timer
        nextTriggerTime = Time.time + retriggerCooldown; // Set cooldown
        
        Debug.Log("DangerZone: Triggering weakpoint hit on " + enemiesInZone.Count + " enemies");

        // Clean up null/dead enemies
        enemiesInZone.RemoveAll(e => e == null || e.health.IsDead);

        if (enemiesInZone.Count == 0)
        {
            if (destroyAfterTrigger)
                Destroy(gameObject);
            return;
        }

        // Hitstop
        if (HitstopManager.instance != null)
            HitstopManager.instance.FreezeTime(hitstopDuration);

        // Play all weakpoint sounds
        if (weakpointAudioSource != null && weakpointHitSounds != null && weakpointHitSounds.Length > 0)
        {
            foreach (var clip in weakpointHitSounds)
            {
                if (clip != null)
                    weakpointAudioSource.PlayOneShot(clip);
            }
        }

        // Camera shake
        if (cameraShaker != null)
            cameraShaker.ApplyShake(shakeIntensity, shakeDuration);

        foreach (Enemy enemy in enemiesInZone)
        {
            if (enemy != null && !enemy.health.IsDead)
            {
                try
                {
                    // Destroy weakpoint hitbox if exists
                    if (destroyWeakpointHitbox && enemy.enemyWeakpoint != null)
                    {
                        enemy.enemyWeakpoint.DestorySpawnedWeakpoint();
                    }

                    // Set weakpoint hit flag and deal damage
                    enemy.health.isDamageByWeakpointHit = true;
                    Debug.Log("DangerZone: Set weakpoint flag to TRUE for " + enemy.name);
                    Debug.Log("DangerZone: Enemy current state: " + enemy.stateMachine.CurrentState.GetType().Name);
                    Debug.Log("DangerZone: useDynamicChaseSpeed = " + enemy.stats.useDynamicChaseSpeed);
                    Debug.Log("DangerZone: About to call Damage with amount = " + weakpointDamage);
                    Debug.Log("DangerZone: Enemy health before damage = " + enemy.health.GetHealthValue());
                    enemy.health.Damage(weakpointDamage);
                    Debug.Log("DangerZone: Damage COMPLETED on " + enemy.name + ", isDead = " + enemy.health.IsDead);
                    Debug.Log("DangerZone: Enemy health after damage = " + enemy.health.GetHealthValue());
                }
                catch (System.Exception e)
                {
                    Debug.LogError("DangerZone: ERROR processing enemy " + enemy.name + ": " + e.Message);
                    Debug.LogError("DangerZone: Stack trace: " + e.StackTrace);
                }
            }
        }

        // Play post hit sound with delay
        StartCoroutine(Co_PlayPostHitSound());

        // Activate objects
        ActivateObjects();

        // Deactivate objects
        DeactivateObjects();

        // Stop spawners
        StopSpawners();

        // Disable specific spawn points
        DisableSpawnPoints();

        if (destroyAfterTrigger)
            Destroy(gameObject, postHitSoundDelay + 1f);
    }

    private void ActivateObjects()
    {
        if (objectsToActivate == null || objectsToActivate.Length == 0) return;

        foreach (var obj in objectsToActivate)
        {
            if (obj != null)
                obj.SetActive(true);
        }

        if (activateSound != null)
            StartCoroutine(Co_PlayActivateSound());
    }

    private IEnumerator Co_PlayActivateSound()
    {
        if (activateSoundDelay > 0)
            yield return new WaitForSecondsRealtime(activateSoundDelay);

        if (postHitAudioSource != null)
            postHitAudioSource.PlayOneShot(activateSound);
        else
            AudioSource.PlayClipAtPoint(activateSound, transform.position);
    }

    private void DeactivateObjects()
    {
        if (objectsToDeactivate == null) return;

        foreach (var obj in objectsToDeactivate)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }

    private void StopSpawners()
    {
        if (spawnersToStop == null) return;

        foreach (var spawner in spawnersToStop)
        {
            if (spawner != null)
            {
                if (despawnEnemiesFromSpawners)
                {
                    spawner.StopSpawningAndDespawn();
                }
                else
                {
                    spawner.StopSpawning();
                }
                
                spawner.StopMusic();
            }
        }
    }

    private void DisableSpawnPoints()
    {
        if (spawnPointsToDisable == null) return;

        foreach (var spawnPoint in spawnPointsToDisable)
        {
            if (spawnPoint != null)
                spawnPoint.SetActive(false);
        }
    }

    private IEnumerator Co_PlayPostHitSound()
    {
        if (postHitSound == null) yield break;

        yield return new WaitForSecondsRealtime(postHitSoundDelay);

        if (postHitAudioSource != null)
        {
            postHitAudioSource.PlayOneShot(postHitSound);
        }
        else
        {
            AudioSource.PlayClipAtPoint(postHitSound, transform.position);
        }
    }

    public void ResetZone()
    {
        hasTriggered = false;
        playerTimeInZone = 0f;
        nextTriggerTime = 0f;
    }

    public float GetTimeRemaining()
    {
        return Mathf.Max(0f, timeUntilTrigger - playerTimeInZone);
    }
}
