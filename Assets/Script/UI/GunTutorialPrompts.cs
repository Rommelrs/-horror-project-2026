using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

// One-off weapon hints shown after picking up the pistol: "Press [RMB] to Aim" until the player
// aims, then "Press [LMB] to Shoot" while aiming, until the first shot is fired. Hook StartTutorial()
// to the pistol pickup's OnInteracted event. Both prompts are InteractPromptUI copies with
// "Register As Instance" turned off, so they don't interfere with the normal interact prompt.
public class GunTutorialPrompts : MonoBehaviour
{
    [SerializeField] InteractPromptUI aimPrompt;
    [SerializeField] InteractPromptUI shootPrompt;

    [Tooltip("Cutscene that plays after the pickup. The prompts wait until it has finished before showing.")]
    [SerializeField] PlayableDirector waitForCutscene;

    [Tooltip("Safety: if the cutscene hasn't started this many seconds after the pickup subtitle ended, show the prompts anyway.")]
    [SerializeField] float cutsceneStartTimeout = 8f;

    [Tooltip("How long the shoot prompt stays after the first shot before fading out.")]
    [SerializeField] float hideDelayAfterShot = 0.5f;

    enum State { None, Aim, Shoot }

    Coroutine routine;
    bool shotFired;
    PlayerWeaponSystem weapon;

    public void StartTutorial()
    {
        if (routine != null)
            return;

        routine = StartCoroutine(Co_Tutorial());
    }

    private void OnDestroy()
    {
        if (weapon != null)
            weapon.OnShotFired -= OnShotFired;
    }

    void OnShotFired()
    {
        shotFired = true;
    }

    IEnumerator Co_Tutorial()
    {
        // Let the pickup's own subtitle / camera moment start before we begin checking for it
        yield return null;

        while (Player.instance == null || Player.instance.playerWeaponSystem == null)
            yield return null;

        weapon = Player.instance.playerWeaponSystem;
        weapon.OnShotFired += OnShotFired;

        if (waitForCutscene != null)
            yield return Co_WaitForCutscene();

        State shown = State.None;

        while (!shotFired)
        {
            State wanted = State.None;

            if (CanShowPrompts())
                wanted = weapon.isAiming ? State.Shoot : State.Aim;

            if (wanted != shown)
            {
                shown = wanted;

                if (shown == State.Aim) aimPrompt.ShowPrompt(); else aimPrompt.HidePrompt();
                if (shown == State.Shoot) shootPrompt.ShowPrompt(); else shootPrompt.HidePrompt();
            }

            yield return null;
        }

        aimPrompt.HidePrompt();
        shootPrompt.HidePrompt(hideDelayAfterShot);

        weapon.OnShotFired -= OnShotFired;
        routine = null;
    }

    // The cutscene starts after the pickup subtitle ends (plus its own short delay), so first wait
    // for it to start, then for it to finish.
    IEnumerator Co_WaitForCutscene()
    {
        float idleTime = 0f;
        while (waitForCutscene.state != PlayState.Playing)
        {
            bool subtitleBusy = SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy();
            if (!subtitleBusy)
                idleTime += Time.unscaledDeltaTime;

            if (idleTime > cutsceneStartTimeout)
                yield break;

            yield return null;
        }

        while (waitForCutscene.state == PlayState.Playing)
            yield return null;
    }

    bool CanShowPrompts()
    {
        if (!weapon.weaponIsEnabled) return false;
        if (Player.instance.IsDead()) return false;
        if (GameManager.IsPaused) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        if (InventoryUI.instance != null && InventoryUI.instance.InventoryIsActive()) return false;
        return true;
    }
}
