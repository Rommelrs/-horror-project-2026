using UnityEngine;

// Put this on something that has solid colliders but can be seen through (a barred gate, a fence, a grate).
// LampGlowOcclusion ignores every collider on this object and its children when it checks whether a lamp glow is hidden,
// so the glow keeps shining through it. Physics (player, enemies, bullets) is not affected at all.
public class LampGlowSeeThrough : MonoBehaviour
{
    public static bool IsSeeThrough(Collider c)
    {
        return c != null && c.GetComponentInParent<LampGlowSeeThrough>() != null;
    }
}
