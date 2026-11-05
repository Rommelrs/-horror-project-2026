using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Small "7 / 78" ammo readout in the bottom-right corner (rounds in the magazine / spare rounds in the
// inventory), with the icon of the weapon that is out beside it. It is only on screen while it is useful: while
// aiming (that is when you shoot), during a reload, and briefly after either ends or after the numbers change
// (e.g. picking up ammo), or after switching weapons. With the pipe out only its icon shows (when it is raised or
// just taken out). The rest of the time it is faded out, so there is no permanent HUD. Switching weapons shows
// a thin vertical line beside the icon, the weapons sliding across with their keys under them (UpdateWheel).
public class AmmoCounterUI : MonoBehaviour
{
    public const string PrefKey = "AmmoCounterEnabled";

    static bool? cachedEnabled;

    /// <summary>Options toggle: whether the counter may appear at all (on by default).</summary>
    public static bool Enabled
    {
        get
        {
            if (cachedEnabled == null)
                cachedEnabled = PlayerPrefs.GetInt(PrefKey, 1) == 1;
            return cachedEnabled.Value;
        }
        set
        {
            cachedEnabled = value;
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
        }
    }

    [SerializeField] Text label;
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Timing")]
    [SerializeField] float fadeInTime = 0.2f;
    [SerializeField] float fadeOutTime = 0.5f;
    [Tooltip("How long it stays after you stop aiming.")]
    [SerializeField] float lingerAfterAim = 0.8f;
    [Tooltip("How long it stays after a reload (so you see the new count).")]
    [SerializeField] float lingerAfterReload = 1.5f;
    [Tooltip("How long it shows when the numbers change while not aiming (e.g. picking up ammo).")]
    [SerializeField] float lingerOnChange = 1.5f;

    [Header("Look")]
    [SerializeField] Color normalColor = new Color(0.91f, 0.91f, 0.79f, 1f);
    [SerializeField] Color lowAmmoColor = new Color(0.85f, 0.27f, 0.22f, 1f);
    [Tooltip("At or below this many rounds in the magazine the magazine number turns red.")]
    [SerializeField] int lowAmmoThreshold = 3;
    [SerializeField] Color reserveColor = new Color(0.72f, 0.72f, 0.62f, 1f);
    [SerializeField] int magazineFontSize = 36;
    [SerializeField] int reserveFontSize = 26;

    [Header("Weapon icon")]
    [SerializeField] Image weaponIcon;
    [SerializeField] Sprite gunIcon;
    [SerializeField] Sprite pipeIcon;
    [Tooltip("How long it shows after switching between the pipe and the gun.")]
    [SerializeField] float lingerAfterSwitch = 2f;

    [Header("Switch wheel")]
    [SerializeField] Image wheelTicks;
    [SerializeField] Image wheelSweep;
    [SerializeField] Image outgoingIcon;
    [SerializeField] Text outgoingNumber;
    [SerializeField] Text keyNumber;
    [SerializeField] string pipeKey = "1";
    [SerializeField] string gunKey = "2";
    [Tooltip("How far a weapon travels to clear the window's edge, the number's drop and how far the ammo numbers step aside (px).")]
    [SerializeField] float exitTravel = 66f;
    [SerializeField] float numberDrop = 38f;
    [SerializeField] float labelShift = 12f;
    [SerializeField] float ringAlpha = 0.28f;
    [SerializeField] float sweepAlpha = 0.6f;
    [SerializeField] float iconAlpha = 0.95f;
    [SerializeField] float ringFadeOut = 0.35f;

    PlayerWeaponSystem weapon;
    float visibleUntil;
    int shownMagazine = -1;
    int shownReserve = -1;
    bool initialised;
    bool switchingToPipe;
    float switchStart, numberUntil, ringFade, pairFade;
    Vector2 iconRest, labelRest;
    bool iconRestSet;

    void OnEnable() => PlayerMeleeWeapon.OnSwitchStarted += OnSwitchStarted;
    void OnDisable() => PlayerMeleeWeapon.OnSwitchStarted -= OnSwitchStarted;
    void OnSwitchStarted(bool toPipe)
    {
        switchingToPipe = toPipe;
        switchStart = Time.time;
    }

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        if (weapon == null && Player.instance != null)
            weapon = Player.instance.playerWeaponSystem;

        // the pipe that is out (or about to be): its icon only
        bool pipe = PlayerMeleeWeapon.Switching ? switchingToPipe : PlayerMeleeWeapon.Equipped;
        bool allowed = weapon != null && Enabled && (weapon.weaponIsEnabled || pipe) && Player.instance != null
            && !Player.instance.IsDead() && !GameManager.IsPaused;

        if (weaponIcon != null)
        {
            weaponIcon.sprite = pipe ? pipeIcon : gunIcon;
            weaponIcon.enabled = weaponIcon.sprite != null;
        }
        label.enabled = !pipe;
        UpdateWheel(pipe, allowed);

        if (allowed && PlayerMeleeWeapon.Switching)
            visibleUntil = Mathf.Max(visibleUntil, Time.unscaledTime + lingerAfterSwitch);

        if (allowed && pipe)
        {
            if (PlayerMeleeWeapon.Ready) visibleUntil = Mathf.Max(visibleUntil, Time.unscaledTime + lingerAfterAim);
            allowed = Time.unscaledTime < visibleUntil;
        }
        else if (allowed)
        {
            float now = Time.unscaledTime;

            if (weapon.isReloading)
                visibleUntil = Mathf.Max(visibleUntil, now + lingerAfterReload);
            else if (weapon.isAiming)
                visibleUntil = Mathf.Max(visibleUntil, now + lingerAfterAim);

            int magazine = weapon.currentAmmo;
            int reserve = weapon.infiniteAmmo ? -2 : weapon.GetReserveAmmo();

            if (magazine != shownMagazine || reserve != shownReserve)
            {
                // the very first read just fills in the text, it isn't a "change"
                if (initialised)
                    visibleUntil = Mathf.Max(visibleUntil, now + lingerOnChange);

                shownMagazine = magazine;
                shownReserve = reserve;
                initialised = true;
                RefreshText(magazine, reserve);
            }

            allowed = now < visibleUntil;
        }

        float target = allowed ? 1f : 0f;
        float duration = allowed ? fadeInTime : fadeOutTime;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
    }

    // On a switch: a thin vertical line between the ammo numbers and the icon, the icon's number under it. An invisible window (WeaponWindow, a mask) cuts everything off
    // at its edges: the weapon taken out comes in from the left as the one put away leaves through the right, each with its number, and
    // then only the one in hand is there. A moment later the line and the number fade, leaving the icon in its usual place. A
    // number key that does nothing brings them back as a reminder.
    void UpdateWheel(bool pipe, bool allowed)
    {
        if (wheelTicks == null || weaponIcon == null) return;
        var iconRect = weaponIcon.rectTransform;
        if (!iconRestSet)
        {
            iconRest = iconRect.anchoredPosition;
            labelRest = label.rectTransform.anchoredPosition;
            iconRestSet = true;
        }

        bool both = PlayerMeleeWeapon.OwnsPipe && Player.instance != null && Player.instance.inventory != null && Player.instance.inventory.HasWeapon();
        bool switching = PlayerMeleeWeapon.Switching;
        float e = switching ? Mathf.Clamp01((Time.time - switchStart) / Mathf.Max(0.01f, PlayerMeleeWeapon.SwitchDuration)) : 1f;
        float s = Mathf.SmoothStep(0f, 1f, e);

        // forgot the key: a number key that does nothing still brings the reminder up
        var kb = Keyboard.current;
        if (allowed && both && kb != null && (kb.digit1Key.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame || kb.digit3Key.wasPressedThisFrame
            || kb.digit4Key.wasPressedThisFrame || kb.digit5Key.wasPressedThisFrame || kb.digit6Key.wasPressedThisFrame
            || kb.digit7Key.wasPressedThisFrame || kb.digit8Key.wasPressedThisFrame || kb.digit9Key.wasPressedThisFrame))
        {
            numberUntil = Time.time + lingerAfterSwitch;
            visibleUntil = Mathf.Max(visibleUntil, Time.unscaledTime + lingerAfterSwitch);
        }
        if (switching) numberUntil = Time.time + lingerAfterSwitch;
        pairFade = Mathf.MoveTowards(pairFade, both && (switching || Time.time < numberUntil) ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);

        // in from the left, out through the right
        iconRect.anchoredPosition = iconRest + Vector2.left * (exitTravel * (1f - s));
        Color c = weaponIcon.color; c.a = iconAlpha; weaponIcon.color = c;
        outgoingIcon.enabled = switching;
        outgoingIcon.sprite = pipe ? gunIcon : pipeIcon;
        outgoingIcon.rectTransform.anchoredPosition = iconRest + Vector2.right * (exitTravel * s);
        c = outgoingIcon.color; c.a = iconAlpha; outgoingIcon.color = c;

        // the number under each
        outgoingNumber.enabled = switching;
        outgoingNumber.text = pipe ? gunKey : pipeKey;
        c = outgoingNumber.color; c.a = 1f; outgoingNumber.color = c;
        keyNumber.text = pipe ? pipeKey : gunKey;
        keyNumber.rectTransform.anchoredPosition = iconRect.anchoredPosition + Vector2.down * numberDrop;
        c = keyNumber.color; c.a = pairFade; keyNumber.color = c;

        // the line stays as long as the number; its brighter part only runs down it during the switch
        ringFade = Mathf.MoveTowards(ringFade, switching ? 1f : 0f, Time.unscaledDeltaTime / (switching ? 0.1f : ringFadeOut));
        c = wheelTicks.color; c.a = ringAlpha * pairFade; wheelTicks.color = c;
        wheelSweep.rectTransform.localScale = new Vector3(1f, e, 1f);
        c = wheelSweep.color; c.a = sweepAlpha * ringFade; wheelSweep.color = c;

        // the ammo numbers keep clear of the window
        label.rectTransform.anchoredPosition = labelRest + Vector2.left * (labelShift * Mathf.SmoothStep(0f, 1f, pairFade));
    }

    void RefreshText(int magazine, int reserve)
    {
        Color magColor = magazine <= lowAmmoThreshold ? lowAmmoColor : normalColor;
        string mag = $"<color=#{ColorUtility.ToHtmlStringRGB(magColor)}><size={magazineFontSize}>{magazine}</size></color>";

        if (reserve == -2)          // infinite ammo: just the magazine
            label.text = mag;
        else
            label.text = $"{mag} <color=#{ColorUtility.ToHtmlStringRGB(reserveColor)}><size={reserveFontSize}>/ {reserve}</size></color>";
    }
}
