using UnityEngine;

// "Press [Tab] to switch target": shown while the pipe is up and two or more enemies are in its reach, because the lock stays on
// the first one. Once the player has switched once the hint is never shown again (StoryFlag, travels with saves, resets on New
// Game). Lives on GameCanvas.prefab next to HealTutorialPrompts.
public class SwitchTargetHint : MonoBehaviour
{
    const string SeenFlag = "TutorialSwitchTarget";

    [SerializeField] InteractPromptUI prompt;
    [Tooltip("At most this long on screen per showing.")]
    [SerializeField] float showSeconds = 8f;
    [Tooltip("After a showing ran out, wait this long before it may show again.")]
    [SerializeField] float cooldown = 25f;
    [Tooltip("The two enemies have to be in reach this long first (not for a passing moment).")]
    [SerializeField] float settle = 0.6f;

    bool on;
    float shownFor, nextAllowed, crowdedFor;

    void OnEnable() => PlayerMeleeWeapon.OnTargetSwitched += OnSwitched;
    void OnDisable() => PlayerMeleeWeapon.OnTargetSwitched -= OnSwitched;

    void OnSwitched()
    {
        StoryFlags.Set(SeenFlag);
        Hide();
    }

    void Update()
    {
        if (prompt == null) return;
        if (StoryFlags.Has(SeenFlag)) { if (on) Hide(); return; }

        var melee = PlayerMeleeWeapon.instance;
        bool crowded = melee != null && PlayerMeleeWeapon.Ready && CanShow() && melee.LockableEnemyCount() >= 2;
        crowdedFor = crowded ? crowdedFor + Time.unscaledDeltaTime : 0f;

        if (!on)
        {
            if (crowdedFor >= settle && Time.unscaledTime >= nextAllowed)
            {
                on = true;
                shownFor = 0f;
                prompt.ShowPrompt();
            }
            return;
        }

        if (!crowded) { Hide(); return; }
        shownFor += Time.unscaledDeltaTime;
        if (shownFor >= showSeconds)
        {
            Hide();
            nextAllowed = Time.unscaledTime + cooldown;
        }
    }

    void Hide()
    {
        if (!on) return;
        on = false;
        prompt.HidePrompt();
    }

    static bool CanShow()
    {
        var p = Player.instance;
        if (p == null || p.IsDead() || p.isGrabbed) return false;
        if (GameManager.IsPaused) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        return true;
    }
}
