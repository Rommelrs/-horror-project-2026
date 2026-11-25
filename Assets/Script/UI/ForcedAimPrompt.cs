using UnityEngine;

/// <summary>
/// "Press [LMB] to Shoot / Press [RMB] to lower the gun" while a scene raises the gun for the player (a playtester thought he was stuck).
/// </summary>
public static class ForcedAimPrompt
{
    static InteractPromptUI shoot, lower;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { shoot = lower = null; }

    public static void Show()
    {
        if (shoot == null) shoot = PromptCopy.Make(null, "ShootPromptUI", null);
        if (lower == null) lower = PromptCopy.Make(null, "AimPromptUI", " to lower the gun", 58f);
        if (shoot != null) shoot.ShowPrompt();
        if (lower != null) lower.ShowPrompt();
    }

    public static void Hide()
    {
        if (shoot != null) shoot.HidePrompt();
        if (lower != null) lower.HidePrompt();
    }
}
