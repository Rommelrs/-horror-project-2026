using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything that can hurt Khalid stands still: while the screen fades to black (doors, pictures, the register...) and while something
/// walks him about (a candy wrapper's auto-walk). Enemy asks this every frame (Enemy.ApplyFreeze); the player's health refuses damage
/// meanwhile, so a bag already in the air does nothing either.
/// </summary>
public static class EnemyFreeze
{
    static readonly HashSet<Object> holders = new HashSet<Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => holders.Clear();

    public static void Hold(Object owner) { if (owner != null) holders.Add(owner); }
    public static void Release(Object owner) { holders.Remove(owner); }

    public static bool Active
    {
        get
        {
            if (FadeScreenUI.ScreenCovered) return true;
            if (holders.Count == 0) return false;
            holders.RemoveWhere(o => o == null);       // owner destroyed without letting go (scene change)
            return holders.Count > 0;
        }
    }
}
