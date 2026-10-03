using UnityEngine;
using UnityEngine.UI;

// Small "7 / 78" ammo readout in the bottom-right corner (rounds in the magazine / spare rounds in the
// inventory). It is only on screen while it is useful: while aiming (that is when you shoot), during a
// reload, and briefly after either ends or after the numbers change (e.g. picking up ammo). The rest of
// the time it is faded out, so there is no permanent HUD.
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

    PlayerWeaponSystem weapon;
    float visibleUntil;
    int shownMagazine = -1;
    int shownReserve = -1;
    bool initialised;

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

        bool allowed = weapon != null && Enabled && weapon.weaponIsEnabled && Player.instance != null
            && !Player.instance.IsDead() && !GameManager.IsPaused;

        if (allowed)
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
