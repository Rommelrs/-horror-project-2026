using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ToolBox.Pools;

public class DamageOnTrigger : MonoBehaviour, IPoolable
{
    [System.Serializable]
    public enum DameOnTriggerType
    {
        TickDamage,
        SingleDamage
    }

    public DameOnTriggerType damageOnTriggerType;
    public float damageTickRate = 0.5f;
    public int damagePerTick = 2;
    public bool slowPlayerMovement = false;
    public float slowDuration = 5f;
    public float damageWindowDuration = 2f; // How long the damage zone is active after spawning (0 or negative = infinite)
    public ElectricityTrapController trapController; // Optional: for electricity trap retreat

    float nextDamageTime;
    bool damaged = false;
    float spawnTime;
    bool damageWindowActive = true;

    private void Awake()
    {
        spawnTime = Time.unscaledTime;
    }

    private void OnEnable()
    {
        // Reset spawn time when the GameObject is enabled
        spawnTime = Time.unscaledTime;
        damaged = false;
        damageWindowActive = true;
    }

    public void OnDepool()
    {
        nextDamageTime = 0f;
        spawnTime = Time.unscaledTime;
        damageWindowActive = true;
    }

    public void OnPool()
    {
        nextDamageTime = 0f;
        damaged = false;
        damageWindowActive = true;
    }

    private void OnTriggerStay(Collider other)
    {
        // Only check player
        if (!other.CompareTag("Player"))
            return;

        // Check if damage window has expired
        if (damageWindowDuration > 0 && Time.unscaledTime > spawnTime + damageWindowDuration)
        {
            damageWindowActive = false;
            return;
        }

        if (!damageWindowActive)
            return;

        if (damaged)
            return;

        if (GameManager.IsPaused)
            return;

        if (Time.unscaledTime < nextDamageTime)
            return;

        // Lock next tick immediately (prevents double calls in same frame)
        nextDamageTime = Time.unscaledTime + damageTickRate;

        Health health = other.GetComponent<Health>();
        if (health == null || health.IsDead)
            return;

        Player player = other.GetComponent<Player>();
        if (player != null && player.isRolling)
            return;

        //Allow only single damage
        if(damageOnTriggerType == DameOnTriggerType.SingleDamage)
            damaged = true;

        // Apply damage
        health.Damage(damagePerTick);

        //Debug.LogError("Player Damaged!");

        // Notify trap controller (for electricity retreat)
        if (trapController != null)
            trapController.OnPlayerDamaged();

        //Slow Player Movement
        if (slowPlayerMovement)
            Player.instance.playerMovementLimiter.LimitMovement(slowDuration);
    }
}
