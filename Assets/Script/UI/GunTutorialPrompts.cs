using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

// One-off weapon hints shown after picking up the first weapon. Hook StartTutorial() to the pickup's OnInteracted event.
//  Gun:   with the pipe in his hands, first "Press [2] or [wheel] to switch weapons" until the gun is out; then
//         "Press [RMB] to Aim" until the player aims, then "Press [LMB] to Shoot" while aiming, until the first shot
//         (or until he lowers the gun without shooting).
//  Melee: "Hold [RMB] to Prepare your attack" until the pipe is raised, then "Press [LMB] to attack" while it is
//         raised, until the first swing (the Store's steel pipe).
// The prompts are InteractPromptUI copies with "Register As Instance" turned off, so they don't interfere with the
// normal interact prompt.
public class GunTutorialPrompts : MonoBehaviour
{
    public enum WeaponKind { Gun, Melee }

    [Tooltip("Gun: aim / shoot with the pistol. Melee: raise / swing the steel pipe.")]
    [SerializeField] WeaponKind kind = WeaponKind.Gun;

    [Tooltip("First prompt (aim / raise the pipe)")]
    [SerializeField] InteractPromptUI aimPrompt;
    [Tooltip("Second prompt (shoot / swing)")]
    [SerializeField] InteractPromptUI shootPrompt;
    [Tooltip("Gun only: shown first while the pipe is still out (switch to the gun).")]
    [SerializeField] InteractPromptUI switchPrompt;

    [Tooltip("Cutscene that plays after the pickup. The prompts wait until it has finished before showing.")]
    [SerializeField] PlayableDirector waitForCutscene;

    [Tooltip("Safety: if the cutscene hasn't started this many seconds after the pickup subtitle ended, show the prompts anyway.")]
    [SerializeField] float cutsceneStartTimeout = 8f;

    [Tooltip("How long the second prompt stays after the first shot / swing before fading out.")]
    [SerializeField] float hideDelayAfterShot = 0.5f;

    enum State { None, Switch, Aim, Shoot }

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
        if (kind == WeaponKind.Gun)
            weapon.OnShotFired += OnShotFired;

        if (waitForCutscene != null)
            yield return Co_WaitForCutscene();

        State shown = State.None;

        while (!shotFired)
        {
            // the first swing counts as the "shot"
            if (kind == WeaponKind.Melee && PlayerMeleeWeapon.Attacking)
            {
                shotFired = true;
                break;
            }

            State wanted = State.None;

            if (kind == WeaponKind.Gun && switchPrompt != null && (PlayerMeleeWeapon.Equipped || PlayerMeleeWeapon.Switching))
                wanted = CanShowSwitchPrompt() ? State.Switch : State.None;
            else if (CanShowPrompts())
                wanted = IsAiming() ? State.Shoot : State.Aim;

            // gun: aiming and lowering it again without shooting means the player chose not to - the hint is done
            if (kind == WeaponKind.Gun && shown == State.Shoot && wanted == State.Aim)
                break;

            if (wanted != shown)
            {
                shown = wanted;

                if (switchPrompt != null) { if (shown == State.Switch) switchPrompt.ShowPrompt(); else switchPrompt.HidePrompt(); }
                if (shown == State.Aim) aimPrompt.ShowPrompt(); else aimPrompt.HidePrompt();
                if (shown == State.Shoot) shootPrompt.ShowPrompt(); else shootPrompt.HidePrompt();
            }

            yield return null;
        }

        if (switchPrompt != null) switchPrompt.HidePrompt();
        aimPrompt.HidePrompt();
        if (shotFired) shootPrompt.HidePrompt(hideDelayAfterShot); else shootPrompt.HidePrompt();

        weapon.OnShotFired -= OnShotFired;
        routine = null;
    }

    bool IsAiming()
    {
        return kind == WeaponKind.Melee ? PlayerMeleeWeapon.Ready : weapon.isAiming;
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

    // the pipe is out (or on its way out / back): the gun can't be aimed yet
    bool CanShowSwitchPrompt()
    {
        if (PlayerMeleeWeapon.Switching) return false;
        if (Player.instance.IsDead()) return false;
        if (GameManager.IsPaused) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        if (InventoryUI.instance != null && InventoryUI.instance.InventoryIsActive()) return false;
        return true;
    }

    bool CanShowPrompts()
    {
        if (kind == WeaponKind.Melee ? !PlayerMeleeWeapon.Equipped : !weapon.weaponIsEnabled || PlayerMeleeWeapon.Equipped || PlayerMeleeWeapon.Switching) return false;
        if (Player.instance.IsDead()) return false;
        if (GameManager.IsPaused) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        if (InventoryUI.instance != null && InventoryUI.instance.InventoryIsActive()) return false;
        return true;
    }
}
