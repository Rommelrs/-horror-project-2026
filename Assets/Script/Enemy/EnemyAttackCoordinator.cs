using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages attack tokens to prevent multiple enemies from attacking simultaneously.
/// This creates fairer combat encounters by limiting concurrent attacks.
/// </summary>
public class EnemyAttackCoordinator : MonoBehaviour
{
    public static EnemyAttackCoordinator Instance { get; private set; }

    [Header("Token Settings")]
    [Tooltip("Maximum number of enemies that can attack simultaneously")]
    [SerializeField] private int maxConcurrentAttacks = 2;
    
    [Tooltip("Maximum time an enemy can hold a token before it's auto-released")]
    [SerializeField] private float tokenTimeout = 5f;
    
    [Tooltip("Minimum time between token requests from the same enemy")]
    [SerializeField] private float requestCooldown = 0.5f;

    private int availableTokens;
    private Dictionary<Enemy, float> activeTokens = new Dictionary<Enemy, float>();
    private Dictionary<Enemy, float> lastRequestTime = new Dictionary<Enemy, float>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        availableTokens = maxConcurrentAttacks;
    }

    private void Update()
    {
        if (activeTokens.Count == 0) return; // Skip if no active tokens
        
        // Check for expired tokens and auto-release them
        List<Enemy> expiredTokens = new List<Enemy>();
        
        foreach (var kvp in activeTokens)
        {
            if (kvp.Key == null || Time.time - kvp.Value > tokenTimeout)
            {
                expiredTokens.Add(kvp.Key);
            }
        }

        foreach (var enemy in expiredTokens)
        {
            ReleaseToken(enemy);
        }
    }

    /// <summary>
    /// Request an attack token. Returns true if token is granted.
    /// </summary>
    public bool RequestAttackToken(Enemy enemy)
    {
        if (enemy == null) return false;

        // Check if this enemy already has a token
        if (activeTokens.ContainsKey(enemy))
        {
            return true;
        }

        // Check cooldown to prevent spam requests
        if (lastRequestTime.ContainsKey(enemy))
        {
            if (Time.time - lastRequestTime[enemy] < requestCooldown)
            {
                return false;
            }
        }

        lastRequestTime[enemy] = Time.time;

        // Check if tokens are available
        if (availableTokens > 0)
        {
            availableTokens--;
            activeTokens[enemy] = Time.time;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Release an attack token when enemy finishes attacking.
    /// </summary>
    public void ReleaseToken(Enemy enemy)
    {
        if (enemy == null) return;

        if (activeTokens.ContainsKey(enemy))
        {
            activeTokens.Remove(enemy);
            availableTokens++;
            availableTokens = Mathf.Clamp(availableTokens, 0, maxConcurrentAttacks);
        }
    }

    /// <summary>
    /// Check if an enemy currently holds a token.
    /// </summary>
    public bool HasToken(Enemy enemy)
    {
        return enemy != null && activeTokens.ContainsKey(enemy);
    }

    /// <summary>
    /// Force release all tokens (useful for game state resets).
    /// </summary>
    public void ReleaseAllTokens()
    {
        activeTokens.Clear();
        availableTokens = maxConcurrentAttacks;
    }

    /// <summary>
    /// Clean up when an enemy is destroyed.
    /// </summary>
    public void OnEnemyDestroyed(Enemy enemy)
    {
        ReleaseToken(enemy);
        if (lastRequestTime.ContainsKey(enemy))
        {
            lastRequestTime.Remove(enemy);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Visual debug info
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.yellow;
        style.fontSize = 12;
        
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            $"Attack Tokens: {availableTokens}/{maxConcurrentAttacks}\nActive: {activeTokens.Count}",
            style
        );
    }
#endif
}
