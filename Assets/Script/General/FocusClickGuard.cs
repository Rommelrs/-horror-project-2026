using UnityEngine;

/// <summary>
/// The click that brings the game window back to the front (after watching a video, alt-tabbing...) must not fire the gun or
/// swing the pipe. Only presses in the short moment right after the window regains focus are swallowed - that is when the
/// focusing click arrives. Nothing else is blocked: a held button never counts as a new press anyway, so there is no
/// "wait for release" state that could get stuck. Weapons ask <see cref="Blocked"/> before firing.
/// </summary>
public class FocusClickGuard : MonoBehaviour
{
    const float graceSeconds = 0.2f;

    static float focusedAt = -100f;

    public static bool Blocked => Time.unscaledTime < focusedAt + graceSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => focusedAt = -100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("FocusClickGuard");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<FocusClickGuard>();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) focusedAt = Time.unscaledTime;
    }
}
