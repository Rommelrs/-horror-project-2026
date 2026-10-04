using UnityEngine;

/// <summary>
/// One shared lock for every scripted scare in the store (fuse-hunt events, storage-room events ...), so two of them are
/// never heard on top of each other and there is always a gap after each one.
/// </summary>
public static class DreadEvents
{
    static bool held;
    static float lockedUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        held = false; lockedUntil = 0f;
        // statics survive scene loads: a lock still held when the scene reloads (death, load game) must not block the next run
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        held = false; lockedUntil = 0f;
    }

    public static bool Free { get { return !held && Time.time >= lockedUntil; } }

    /// <summary>Take the lock if nothing else is playing. Release it when the event is over.</summary>
    public static bool TryAcquire()
    {
        if (!Free) return false;
        held = true;
        return true;
    }

    /// <summary>For priority scenes (the register): ignores the quiet gap after the last event, but still never starts on top of one that is playing.</summary>
    public static bool TryAcquireIgnoringGap()
    {
        if (held) return false;
        held = true;
        return true;
    }

    /// <summary>Give the lock back and keep everything quiet for 'gap' seconds.</summary>
    public static void Release(float gap)
    {
        held = false;
        lockedUntil = Mathf.Max(lockedUntil, Time.time + gap);
    }

    /// <summary>Keep new events from starting for the next 'seconds' (does not interrupt a running one).</summary>
    public static void HoldFor(float seconds)
    {
        lockedUntil = Mathf.Max(lockedUntil, Time.time + seconds);
    }
}
