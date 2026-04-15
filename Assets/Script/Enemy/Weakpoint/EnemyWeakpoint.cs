using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WeakpointConfig
{
    [Tooltip("Bone transform to attach weakpoint to")]
    public Transform boneTransform;
    
    [Tooltip("Weakpoint prefab to spawn")]
    public GameObject weakpointPrefab;
    
    [Header("Customization (Optional)")]
    [Tooltip("Local position offset from bone")]
    public Vector3 localPositionOffset = Vector3.zero;
    
    [Tooltip("Local rotation offset from bone")]
    public Vector3 localRotationOffset = Vector3.zero;
    
    [Tooltip("Local scale (default is 1,1,1)")]
    public Vector3 localScale = Vector3.one;
}

public class EnemyWeakpoint : MonoBehaviour
{
    [Header("Weakpoint Configuration")]
    [Tooltip("Configure weakpoints with custom bones, prefabs, and transforms")]
    public WeakpointConfig[] weakpointConfigs;
    
    [Header("Legacy Support")]
    [Tooltip("Old single-prefab system (deprecated - use weakpointConfigs instead)")]
    public GameObject legacyWeakpointPrefab;
    public Transform[] legacyBoneTransforms;

    public enum WeakpointType
    {
        Fixed,      // Spawn one random weakpoint
        Sequential, // Spawn weakpoints in order, cycling through
        Flashing,   // Cycle through weakpoints automatically over time
        AllActive   // Spawn all weakpoints at once
    }

    [Header("Behavior")]
    public WeakpointType weakpointType = WeakpointType.Fixed;
    
    [Tooltip("Only for Flashing type - how often to switch weakpoints")]
    public float flashingRate = 2f;
    
    [Header("Auto Activation")]
    public bool autoShowWeakpointOnStart = false;

    private List<GameObject> spawnedWeakpoints = new List<GameObject>();
    private Coroutine flashingCoroutine;
    private int currentWeakpointIndex = 0;

    private void Start()
    {
        if (autoShowWeakpointOnStart)
            SpawnEnemyWeakpoint();
    }

    /// <summary>
    /// Sets bone transforms for legacy single-prefab system
    /// </summary>
    public void SetBoneTransform(Transform[] newBoneTransforms)
    {
        legacyBoneTransforms = newBoneTransforms;
        
        // Auto-convert to config system if needed
        if ((weakpointConfigs == null || weakpointConfigs.Length == 0) && legacyWeakpointPrefab != null)
        {
            ConvertLegacyToConfigs();
        }
    }
    
    /// <summary>
    /// Converts legacy bone array to WeakpointConfig array
    /// </summary>
    private void ConvertLegacyToConfigs()
    {
        if (legacyBoneTransforms == null || legacyBoneTransforms.Length == 0)
            return;
            
        weakpointConfigs = new WeakpointConfig[legacyBoneTransforms.Length];
        
        for (int i = 0; i < legacyBoneTransforms.Length; i++)
        {
            weakpointConfigs[i] = new WeakpointConfig
            {
                boneTransform = legacyBoneTransforms[i],
                weakpointPrefab = legacyWeakpointPrefab,
                localPositionOffset = Vector3.zero,
                localRotationOffset = Vector3.zero,
                localScale = Vector3.one
            };
        }
    }

    /// <summary>
    /// Spawns weakpoint(s) based on the selected type
    /// </summary>
    public void SpawnEnemyWeakpoint()
    {
        // Convert legacy system if needed
        if ((weakpointConfigs == null || weakpointConfigs.Length == 0) && legacyBoneTransforms != null && legacyWeakpointPrefab != null)
        {
            ConvertLegacyToConfigs();
        }
        
        if (weakpointConfigs == null || weakpointConfigs.Length == 0)
            return;

        switch (weakpointType)
        {
            case WeakpointType.Fixed:
                SpawnRandomWeakpoint();
                break;
                
            case WeakpointType.Sequential:
                SpawnSequentialWeakpoint();
                break;
                
            case WeakpointType.Flashing:
                StartFlashingWeakpoints();
                break;
                
            case WeakpointType.AllActive:
                SpawnAllWeakpoints();
                break;
        }
    }
    
    /// <summary>
    /// Spawns one random weakpoint
    /// </summary>
    private void SpawnRandomWeakpoint()
    {
        if (weakpointConfigs.Length > 0)
        {
            int randomIndex = Random.Range(0, weakpointConfigs.Length);
            SpawnWeakpointFromConfig(weakpointConfigs[randomIndex]);
        }
    }
    
    /// <summary>
    /// Spawns weakpoints sequentially in order, cycling through the array
    /// </summary>
    private void SpawnSequentialWeakpoint()
    {
        if (weakpointConfigs.Length > 0)
        {
            SpawnWeakpointFromConfig(weakpointConfigs[currentWeakpointIndex]);
            // Index is advanced manually via AdvanceSequentialIndex() when weakpoint is hit
        }
    }
    
    /// <summary>
    /// Manually advances to the next sequential weakpoint index (call when weakpoint is successfully hit)
    /// </summary>
    public void AdvanceSequentialIndex()
    {
        currentWeakpointIndex++;
        if (currentWeakpointIndex >= weakpointConfigs.Length)
            currentWeakpointIndex = 0;
    }
    
    /// <summary>
    /// Spawns all configured weakpoints
    /// </summary>
    private void SpawnAllWeakpoints()
    {
        foreach (WeakpointConfig config in weakpointConfigs)
        {
            SpawnWeakpointFromConfig(config);
        }
    }
    
    /// <summary>
    /// Spawns a weakpoint from a config with custom positioning and scale
    /// </summary>
    private void SpawnWeakpointFromConfig(WeakpointConfig config)
    {
        if (config.boneTransform == null || config.weakpointPrefab == null)
            return;
            
        // Instantiate as child of bone (this ensures it follows bone animation)
        GameObject weakpointObj = Instantiate(config.weakpointPrefab, config.boneTransform);
        
        // Apply custom transforms
        weakpointObj.transform.localPosition = config.localPositionOffset;
        weakpointObj.transform.localRotation = Quaternion.Euler(config.localRotationOffset);
        weakpointObj.transform.localScale = config.localScale;
        
        spawnedWeakpoints.Add(weakpointObj);
    }
    
    /// <summary>
    /// Starts cycling through weakpoints
    /// </summary>
    private void StartFlashingWeakpoints()
    {
        if (flashingCoroutine != null)
            StopCoroutine(flashingCoroutine);
            
        flashingCoroutine = StartCoroutine(Co_FlashWeakpoints());
    }

    /// <summary>
    /// Destroys all spawned weakpoints
    /// </summary>
    public void DestorySpawnedWeakpoint()
    {
        // Stop flashing if active
        if (flashingCoroutine != null)
        {
            StopCoroutine(flashingCoroutine);
            flashingCoroutine = null;
        }
        
        // Destroy all spawned weakpoints
        foreach (GameObject weakpoint in spawnedWeakpoints)
        {
            if (weakpoint != null)
                Destroy(weakpoint);
        }
        
        spawnedWeakpoints.Clear();
    }

    /// <summary>
    /// Cycles through weakpoints, spawning one at a time
    /// </summary>
    private IEnumerator Co_FlashWeakpoints()
    {
        currentWeakpointIndex = 0;
        
        while (true)
        {
            // Destroy old weakpoints
            foreach (GameObject weakpoint in spawnedWeakpoints)
            {
                if (weakpoint != null)
                    Destroy(weakpoint);
            }
            spawnedWeakpoints.Clear();
            
            // Spawn current weakpoint
            if (currentWeakpointIndex < weakpointConfigs.Length)
            {
                SpawnWeakpointFromConfig(weakpointConfigs[currentWeakpointIndex]);
            }
            
            // Move to next
            currentWeakpointIndex++;
            if (currentWeakpointIndex >= weakpointConfigs.Length)
                currentWeakpointIndex = 0;
            
            yield return new WaitForSeconds(flashingRate);
        }
    }

    /// <summary>
    /// Returns all currently spawned weakpoint GameObjects
    /// </summary>
    public List<GameObject> GetSpawnedWeakpoints()
    {
        return spawnedWeakpoints;
    }
}
