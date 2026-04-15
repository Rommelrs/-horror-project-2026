using UnityEngine;

/// <summary>
/// Forwards animation events from child Animator to parent Enemy component.
/// Attach this to the GameObject that has the Animator component.
/// </summary>
public class EnemyAnimationEventForwarder : MonoBehaviour
{
    private Enemy enemy;

    private void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
        
        if (enemy == null)
        {
            Debug.LogError($"EnemyAnimationEventForwarder on {gameObject.name}: Could not find Enemy component in parent!");
        }
    }

    // Animation event methods - forward to Enemy
    public void OnFootstep()
    {
        if (enemy != null)
        {
            enemy.PlayFootstep();
        }
    }
    
    public void PlayFootstep()
    {
        if (enemy != null)
        {
            enemy.PlayFootstep();
        }
    }

    public void OnAttack()
    {
        if (enemy != null)
        {
            enemy.AttackHit();
        }
    }
    
    public void PlayThrowSound()
    {
        if (enemy != null)
        {
            enemy.PlayThrowSound();
        }
    }
    
    public void StopThrowSound()
    {
        if (enemy != null)
        {
            enemy.StopThrowSound();
        }
    }
}
