using System.Collections.Generic;
using UnityEngine;

// Named one-way story flags (e.g. "CowardMeeting1Done", "Alley2Visited").
// They are stored in the save system's used-interactables list with a "flag:" prefix, so manual saves and checkpoints
// carry them without any new save format. A session copy is kept as well: a checkpoint restore (death) rolls the save
// system back to an older list, but a flag set since then (a cutscene that already played) must stay set.
public static class StoryFlags
{
    const string Prefix = "flag:";
    static readonly HashSet<string> session = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => session.Clear();

    /// <summary>Forget the session copies (new game / loading a save: the save's own list is the truth again).</summary>
    public static void ResetSession() => session.Clear();

    public static bool Has(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return false;
        if (session.Contains(flag)) return true;
        return SaveManager.instance != null && SaveManager.instance.IsInteractableUsed(Prefix + flag);
    }

    public static void Set(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return;
        session.Add(flag);
        if (SaveManager.instance != null) SaveManager.instance.RegisterUsedInteractable(Prefix + flag);
    }
}
