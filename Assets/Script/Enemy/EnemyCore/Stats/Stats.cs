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

    [Header("Reaction Time")]
    [Tooltip("Random time (min..max seconds) spent in Idle before an Aggressive/Wandering enemy reacts.")]
    public float idleMinDuration = 1f;
    public float idleMaxDuration = 3f;
    [Tooltip("Max time the enemy stands still turning towards the player when a chase starts. The turn can end it earlier (see Chase Start Turn Speed).")]
    public float chaseStartLookDuration = 2f;
    [Tooltip("Degrees per second used to turn towards the player at chase start. 0 = old slow lerp (uses Rotation Smoothness). >0 = he starts running as soon as he faces the player.")]
    public float chaseStartTurnSpeed = 0f;

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
    
    [Header("Direct Approach / Queueing")]
    [Tooltip("ON: head straight for the player (no random offset point around him). OFF: old advanced pathfinding with offset points.")]
    public bool chaseDirectApproach = false;
    [Tooltip("Chance (0-100) each decision that this enemy lines up BEHIND a teammate that is closer to the player instead of taking its own route. Needs Direct Approach.")]
    [Range(0, 100)] public int queueBehindChance = 0;
    [Tooltip("Only teammates within this distance can be followed.")]
    public float queueBehindRange = 6f;
    [Tooltip("How far behind the teammate this enemy stays.")]
    public float queueBehindSpacing = 1.6f;
    [Tooltip("Seconds between queue decisions (randomised +-30%).")]
    public float queueDecisionInterval = 3f;

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
    [Tooltip("Scales how far a WEAK POINT hit pushes this enemy (on top of Knock Back Force x the player's weak point multiplier). 1 = unchanged, 0.5 = half the push.")]
    public float weakpointKnockbackScale = 1f;
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
    [Tooltip("Fixed enemies only: a gunshot he hears (needs Can Hear Gunshots) turns him from a stationary sentry into an Aggressive enemy that chases the player from then on. Stays Fixed until then.")]
    public bool becomesAggressiveWhenAlerted = false;

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
    [Tooltip("ON: HeadshotEnemy only sidesteps as a reaction to the player aiming at it (Side Step Chance is rolled each time). OFF: old behaviour, a blind roll once per second.")]
    public bool sideStepOnlyWhenAimedAt = false;
    [Tooltip("How close (degrees) the player's crosshair has to be to the enemy to count as aiming at it.")]
    public float sideStepAimAngle = 12f;
    [Tooltip("Seconds the player has to keep aiming at the enemy before it reacts.")]
    public float sideStepReactionTime = 0.25f;
    [Tooltip("Seconds between rolls while the player keeps aiming at it and a roll failed.")]
    public float sideStepRerollInterval = 1.5f;
    [Tooltip("Random speed variation per sidestep: 0.25 = anywhere from 75% to 125% of Side Step Speed.")]
    [Range(0f, 0.9f)] public float sideStepSpeedVariation = 0f;
    [Header("Sidewalk (sidestep with matching animation)")]
    [Tooltip("0 = old physics sidestep. >0 = SIDEWALK: he walks sideways at a smooth speed and the strafe animation plays faster/slower so the feet match the ground. This is the ground speed (m/s) the strafe clip covers at playback speed 1 (Questioner clip is about 0.15).")]
    public float sideStepAnimGroundSpeed = 0f;
    [Tooltip("Lowest / highest strafe animation playback speed. The top limit also caps how fast he can sidewalk (ground speed x max).")]
    public float sideStepMinAnimSpeed = 0.5f;
    public float sideStepMaxAnimSpeed = 6f;
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
