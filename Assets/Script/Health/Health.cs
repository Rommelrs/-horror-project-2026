using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour, IDamagable
{
    public delegate void DamageTaken(int damage);
    public DamageTaken OnDamageTaken;

    public UnityEvent OnDeath;
    public bool IsDead = false;

    [SerializeField] int health = 100;
    [SerializeField] int maxHealth = 100;
    [SerializeField] Vector3 hitBoxCenter;

    [Header("UI")]
    [SerializeField] HealthBar healthBar;
    [SerializeField] HealthUIBehaviour healthUIBehaviour;
    [SerializeField] float dynamicShowDuration = 6f;
    [SerializeField] bool hideOnDeath = true;

    [Header("Hit Effects")]
    [SerializeField] ParticleSystem hitParticleEffect;
    [SerializeField] bool spawnHitEffectAtHitPoint = true;
    [SerializeField] AudioClip[] hitSoundEffects;
    [SerializeField] float hitSoundVolume = 2f;

    int IDamagable.Health
    {
        get => health;
        set => health = value;
    }

    public int GetHealthValue()
    {
        return health;
    }

    public int GetMaxHealthValue()
    {
        return maxHealth;
    }

    Vector3 damageDirection = Vector3.zero;
    Coroutine showHealthBarCR;
    float damageImpactForce = 1f;
    public float DamageImpactForce
    {
        get => damageImpactForce;
        set => damageImpactForce = value;
    }

    private void Start()
    {
        // Initialize health bar
        if (healthBar != null)
        {
            healthBar.SetHealth(health, maxHealth, false);

            // Enable or disable health bar based on UI behaviour
            if (healthUIBehaviour == HealthUIBehaviour.Static)
                healthBar.EnableHealthBar(true);
            else
                healthBar.EnableHealthBar(false);
        }
    }

    public bool isDamageByWeakpointHit = false;
    Vector3 lastHitPoint = Vector3.zero;
    
    [Header("Invincibility Frames (Player Only)")]
    [SerializeField] float iFrameDuration = 1f;
    [SerializeField] bool enableIFrames = false;
    private float lastDamageTime = -999f;

    //Damage Behaviour
    public void Damage(int damage)
    {
        // Check for i-frames (invincibility frames)
        if (enableIFrames && Time.time - lastDamageTime < iFrameDuration)
        {
            return; // Ignore damage during i-frames
        }
        
        lastDamageTime = Time.time;
        
        health -= damage;
        if (health <= 0) health = 0;

        if (healthBar != null)
            healthBar.SetHealth(health, maxHealth, true);

        // Spawn hit particle effect
        SpawnHitEffect();

        if (health <= 0 && !IsDead)
        {
            IsDead = true;
            //Trigger Death Event
            OnDeath?.Invoke();

            if (hideOnDeath && healthBar != null)
                healthBar.EnableHealthBar(false);
        }

        //Trigger Event
        if(!IsDead) OnDamageTaken?.Invoke(damage);

        if (healthUIBehaviour == HealthUIBehaviour.Dynamic)
        {
            if (showHealthBarCR != null) StopCoroutine(showHealthBarCR);
            showHealthBarCR = StartCoroutine(Co_ShowHealthBar());
        }
    }

    // Healing Behaviour
    public void Heal(int heal)
    {
        health += heal;
        if (health > maxHealth) health = maxHealth;

        if (healthBar != null)
            healthBar.SetHealth(health, maxHealth, false);
    }

    // Coroutine to show health bar for a limited time
    IEnumerator Co_ShowHealthBar()
    {
        if (healthBar != null)
            healthBar.EnableHealthBar(true);
        yield return new WaitForSeconds(dynamicShowDuration);
        if (healthBar != null)
            healthBar.EnableHealthBar(false);
    }

    //Return damage direction
    public Vector3 GetDamageDirection()
    {
        return damageDirection;
    }

    //Set damage direction
    public void SetDamageDirection(Vector3 direction)
    {
        damageDirection = direction;
    }

    //Set last hit point
    public void SetLastHitPoint(Vector3 hitPoint)
    {
        lastHitPoint = hitPoint;
    }

    //Spawn hit particle effect
    void SpawnHitEffect()
    {
        Vector3 spawnPosition = spawnHitEffectAtHitPoint && lastHitPoint != Vector3.zero 
            ? lastHitPoint 
            : GetCenterOfHitBox();

        // Spawn particle effect
        if (hitParticleEffect != null)
        {
            ParticleSystem ps = Instantiate(hitParticleEffect, spawnPosition, Quaternion.identity);
            Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
        }

        // Play hit sound at player position (for better audibility)
        if (hitSoundEffects != null && hitSoundEffects.Length > 0)
        {
            AudioClip clip = hitSoundEffects[UnityEngine.Random.Range(0, hitSoundEffects.Length)];
            Vector3 soundPos = Player.instance != null ? Player.instance.transform.position : spawnPosition;
            AudioSource.PlayClipAtPoint(clip, soundPos, hitSoundVolume);
        }
    }

    //Reset health to max value
    public void ResetHealth()
    {
        health = maxHealth;
        IsDead = false;

        //Update Health Bar
        if (healthBar != null)
        {
            healthBar.EnableHealthBar(true);
            healthBar.SetHealth(health, maxHealth, false);
        }

        isDamageByWeakpointHit = false;
    }

    //Get the center of the hitbox
    public Vector3 GetCenterOfHitBox()
    {
        return transform.position + hitBoxCenter;
    }

    //Enable or disable health bar
    public void ShowHealthBar(bool value)
    {
        if (healthBar != null)
            healthBar.EnableHealthBar(value);
    }
}

[System.Serializable]
public enum HealthUIBehaviour
{
    Static,
    Dynamic
}
