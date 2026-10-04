using UnityEngine;

/// <summary>
/// The Runner's animation clips fire events (footsteps, attack hits ...) that normally go to the Enemy component.
/// The puppet has no Enemy, so this swallows them (footstep sounds are played by the sequence itself).
/// </summary>
public class PuppetAnimationEventSink : MonoBehaviour
{
    public void OnFootstep() { }
    public void PlayFootstep() { }
    public void OnAttack() { }
    public void PlayThrowSound() { }
    public void StopThrowSound() { }
}
