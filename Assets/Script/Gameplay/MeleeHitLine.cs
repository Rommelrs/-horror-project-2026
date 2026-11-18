using UnityEngine;

/// <summary>
/// PROTOTYPE (Block 2 wardrobe): hitting this with the hammer/pipe (PlayerMeleeWeapon's wall hit sends OnMeleeHit up from
/// the collider it struck) shows a line, as long as the cloth it belongs to is still there.
/// </summary>
public class MeleeHitLine : MonoBehaviour
{
    [Tooltip("The cloth seal: once it has been cut, hits say nothing.")]
    [SerializeField] BurnableObstacle obstacle;
    [SerializeField] SubtitleTrigger line;

    void OnMeleeHit()
    {
        if (line == null) return;
        if (obstacle != null && !obstacle.CanUseLighter()) return;          // already cut
        var subs = SubtitleManager.instance;
        if (subs != null && (subs.IsSubtitleBusy() || subs.IsWithinCooldownPeriod())) return;
        line.TriggerSubtitle();
    }
}
