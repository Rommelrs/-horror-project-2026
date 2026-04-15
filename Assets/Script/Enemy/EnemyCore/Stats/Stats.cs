using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stats : MonoBehaviour
{
    [Header("Generals")]
    public EnemyType enemyType;
    public AttackType attackType;
    public int armor = 0;
    public int enemyFOV = 45;
    public bool disableEnemyAfterDeath = true;
    public float releaseTime = 4f;
    public float rotationSmoothness = 0.2f;

    [Header("Detection")]
    public DetectionMode detectionMode = DetectionMode.FOVCone;
    public float visionRadius = 12f;

    [Header("Speed")]
    public float movementSpeed = 2.5f;
    
    [Header("Pathfinding")]
    [Tooltip("Enable advanced positioning system (spreading, offset calculation). Disable for basic direct chase behavior.")]
    public bool useAdvancedPathfinding = true;
    [Tooltip("Use optimized pathfinding (recommended when advanced pathfinding is enabled). Only applies if useAdvancedPathfinding is true.")]
    public bool useOptimizedPathfinding = true;
    
    [Header("Dynamic Chase Speed")]
    public bool useDynamicChaseSpeed = false;
    [Tooltip("Distance at which enemy starts sprinting")]
    public float sprintDistanceThreshold = 10f;
    [Tooltip("Speed multiplier when sprinting (far from player)")]
    public float sprintSpeedMultiplier = 1.5f;
    [Tooltip("Distance at which enemy slows down")]
    public float slowDistanceThreshold = 5f;
    [Tooltip("Speed multiplier when close to player")]
    public float slowSpeedMultiplier = 0.8f;

    [Header("Knockback")]
    public float knockBackForce = 1.5f;
    public float stillThreshold = 0.2f;
    public float maxKnockbackTime = 1f;
    public bool canHeavyKnockBack = false;
    public float heavyKnockBackDuration = 3f;
    public float wakeUpDuration = 2f;

    [Header("Wandering Enemy Setup")]
    public Transform[] waypoints;
    public float idleHoldBetweenWaypointDuration = 0.5f;
    public float wanderingSpeed = 0.8f;

    [Header("Sound Investigation")]
    public bool canHearGunshots = true;
    public float investigateDuration = 5f;

    [Header("Atack")]
    public int damage = 20;
    public int attackRate = 20;
    public int []attackIndexes;
    public float attackRange = 1.5f;
    public float attackDelay = 0.2f;
    public float attackCooldown = 2f;
    public float criticalHitChance = 0.1f;
    public float criticalHitMultiplier = 2.0f;

    [Header("DashAtack")]
    public float dashTargetOffset = 5f;
    public float dashAttackInitialDelay = 0.5f;
    public float dashAttackDuration = 0.5f;
    public float dashAcceleration = 50f;
    public float dashSpeed = 10f;
    [Tooltip("Delay before checking distance (allows animator transition)")]
    public float dashAnimTransitionDelay = 0.3f;
    [Tooltip("Distance threshold from player to switch to simple attack instead of dash")]
    public float dashToAttackSwitchRange = 1f;

    [Header("Sound Effects")]
    public AudioClip[] takeDamageSFX;
    public AudioClip[] attackSFX;
    public AudioClip[] stabSFX;
    public AudioClip[] randomSFX;
    public float randomMinDuration = 10f;
    public float randomMaxDuration = 20f;
    [Tooltip("Guaranteed sound played during attack animation (via Animation Event)")]
    public AudioClip guaranteedAttackSound;
    
    [Header("Footsteps")]
    public AudioClip[] footstepSounds;
    [Range(0f, 1f)] public float footstepVolume = 0.5f;

    [Header("Animation")]
    public float animationSmoothness = 1f;

    [Header("Sidestep")]
    public float sideStepLookRotation = 30f;
    public float sideStepAccelaration = 2f;
    public float sideStepSpeed = 5f;
    public float sideStepDurationMin = 1f;
    public float sideStepDurationMax = 4f;
    public float sideStepCooldown = 3f;
    [Range(0, 100)] public int sideStepChance = 40;
}

[System.Serializable]
public enum DetectionMode
{
    FOVCone,        // Only detects in FOV cone (realistic)
    SphereRadius    // Detects in 360 degree sphere (omniscient)
}

// Helper class for detection
public static class EnemyDetectionHelper
{
    public static bool CheckDetection(Enemy enemy)
    {
        if (enemy.stats.detectionMode == DetectionMode.FOVCone)
            return enemy.PlayerInRange(enemy.stats.visionRadius) && enemy.PlayerInFOV();
        else
            return enemy.PlayerInRange(enemy.stats.visionRadius);
    }
}
