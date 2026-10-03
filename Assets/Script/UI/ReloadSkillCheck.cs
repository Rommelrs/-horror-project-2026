using UnityEngine;
using UnityEngine.UI;

// PROTOTYPE - reload skill check ("active reload").
//
// While a reload is in progress a marker sweeps back and forth across a small bar. Pressing the reload key
// again stops it: inside the gold zone is a PERFECT, inside the wide zone a GOOD, anywhere else a MISS.
// Perfect and Good cut what is left of the reload down to a fraction of the wait (the animation speeds up
// to match); a miss (or ignoring the bar) just gives the normal stability-based reload, so the skill check is
// a pure bonus and never a penalty.
//
// Stability only makes the BAR harder - the marker is faster and the zones smaller, and the whole bar shakes
// (visual only) - but the marker always moves at a constant speed, so the timing stays fair. In return the
// reward is bigger for shaky hands in absolute terms, because their reload is so much longer.
public class ReloadSkillCheck : MonoBehaviour
{
    public const string PrefKey = "ReloadSkillCheckEnabled";

    static bool? cachedEnabled;
    /// <summary>Accessibility/options switch: off = the bar never appears and reloads behave as before.</summary>
    public static bool Enabled
    {
        get
        {
            if (cachedEnabled == null) cachedEnabled = PlayerPrefs.GetInt(PrefKey, 1) == 1;
            return cachedEnabled.Value;
        }
        set { cachedEnabled = value; PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); }
    }

    [Header("UI")]
    [SerializeField] CanvasGroup group;
    [SerializeField] RectTransform bar;
    [SerializeField] RectTransform goodZone;
    [SerializeField] RectTransform perfectZone;
    [SerializeField] RectTransform marker;
    [SerializeField] Image markerImage;
    [SerializeField] Text resultLabel;

    [Header("Reward  (what is LEFT of the reload after you hit it: 0.15 = only 15% of the remaining wait)")]
    [Tooltip("Perfect hit while fully steady (stability 100).")]
    [SerializeField, Range(0.02f, 1f)] float perfectRemainingSteady = 0.15f;
    [Tooltip("Perfect hit while shaking badly (stability 0).")]
    [SerializeField, Range(0.02f, 1f)] float perfectRemainingShaky = 0.40f;
    [Tooltip("Good hit while fully steady.")]
    [SerializeField, Range(0.02f, 1f)] float goodRemainingSteady = 0.50f;
    [Tooltip("Good hit while shaking badly.")]
    [SerializeField, Range(0.02f, 1f)] float goodRemainingShaky = 0.70f;
    [Tooltip("The reload never speeds up past this multiple (keeps the animation from looking like a glitch).")]
    [SerializeField] float maxSpeedScale = 8f;

    [Header("Difficulty  (steady -> shaky)")]
    [Tooltip("Seconds for the marker to cross the bar once. Constant for the whole reload.")]
    [SerializeField] float sweepTimeSteady = 1.0f;
    [SerializeField] float sweepTimeShaky = 0.5f;
    [Tooltip("Zone sizes as a fraction of the bar width.")]
    [SerializeField] float perfectWidthSteady = 0.09f;
    [SerializeField] float perfectWidthShaky = 0.05f;
    [SerializeField] float goodWidthSteady = 0.24f;
    [SerializeField] float goodWidthShaky = 0.15f;
    [Tooltip("Bar jitter in pixels when shaky. Visual only - it never changes the timing.")]
    [SerializeField] float shakyBarJitter = 5f;

    [Header("Feedback")]
    [SerializeField] Color perfectColor = new Color(1f, 0.82f, 0.28f, 1f);
    [SerializeField] Color goodColor = new Color(0.91f, 0.91f, 0.79f, 1f);
    [SerializeField] Color missColor = new Color(0.6f, 0.35f, 0.33f, 1f);
    [SerializeField] float resultLinger = 0.6f;
    [SerializeField] float fadeTime = 0.2f;

    enum State { Hidden, Running, Locked }

    PlayerWeaponSystem weapon;
    State state = State.Hidden;
    bool wasReloading;
    int beginFrame;

    float markerPos;        // 0..1 across the bar
    float markerDir = 1f;
    float stability01;
    float perfectWidth, goodWidth, zoneCenter, sweepTime;
    float lockedTimer;
    Vector2 barHome;

    private void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        if (bar != null) barHome = bar.anchoredPosition;
    }

    private void Update()
    {
        if (weapon == null && Player.instance != null)
            weapon = Player.instance.playerWeaponSystem;

        if (weapon != null)
        {
            bool reloading = weapon.isReloading;

            if (reloading && !wasReloading && Enabled)
                Begin();
            else if (!reloading && wasReloading)
                state = State.Hidden;       // reload ended (finished or cancelled): fade the bar out

            wasReloading = reloading;
        }

        switch (state)
        {
            case State.Running:
                MoveMarker();
                // ignore the press that started the reload itself
                if (Time.frameCount > beginFrame && weapon.ReloadPressedThisFrame)
                    LockIn();
                break;

            case State.Locked:
                lockedTimer -= Time.unscaledDeltaTime;
                break;
        }

        float target = (state == State.Hidden || (state == State.Locked && lockedTimer <= 0f)) ? 0f : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
    }

    void Begin()
    {
        state = State.Running;
        beginFrame = Time.frameCount;

        stability01 = weapon.StabilityFraction;
        float shaky = 1f - stability01;

        sweepTime = Mathf.Lerp(sweepTimeSteady, sweepTimeShaky, shaky);
        perfectWidth = Mathf.Lerp(perfectWidthSteady, perfectWidthShaky, shaky);
        goodWidth = Mathf.Max(perfectWidth + 0.02f, Mathf.Lerp(goodWidthSteady, goodWidthShaky, shaky));

        // random spot each time so it can't be memorised; keep the whole good zone on the bar
        float margin = goodWidth * 0.5f + 0.03f;
        zoneCenter = Random.Range(Mathf.Max(margin, 0.3f), 1f - margin);

        markerPos = 0f;
        markerDir = 1f;

        float w = bar.rect.width;
        SetZone(goodZone, zoneCenter, goodWidth, w);
        SetZone(perfectZone, zoneCenter, perfectWidth, w);
        PlaceMarker();

        markerImage.color = Color.white;
        resultLabel.text = "";
        bar.anchoredPosition = barHome;
    }

    static void SetZone(RectTransform zone, float center01, float width01, float barWidth)
    {
        zone.anchorMin = zone.anchorMax = new Vector2(0.5f, 0.5f);
        zone.pivot = new Vector2(0.5f, 0.5f);
        zone.sizeDelta = new Vector2(width01 * barWidth, zone.sizeDelta.y);
        zone.anchoredPosition = new Vector2((center01 - 0.5f) * barWidth, 0f);
    }

    void MoveMarker()
    {
        // constant speed: shaky only means faster (and a trembling bar), never a changing speed
        markerPos += markerDir * Time.deltaTime / Mathf.Max(0.05f, sweepTime);
        if (markerPos >= 1f) { markerPos = 1f; markerDir = -1f; }
        else if (markerPos <= 0f) { markerPos = 0f; markerDir = 1f; }
        PlaceMarker();

        // shaky hands: the whole bar (and the marker with it) trembles - purely visual
        float shake = (1f - stability01) * shakyBarJitter;
        if (shake > 0.01f)
        {
            float t = Time.unscaledTime * 38f;
            bar.anchoredPosition = barHome + new Vector2((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * shake, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * shake);
        }
    }

    void PlaceMarker()
    {
        marker.anchoredPosition = new Vector2((markerPos - 0.5f) * bar.rect.width, 0f);
    }

    void LockIn()
    {
        state = State.Locked;
        lockedTimer = resultLinger;
        bar.anchoredPosition = barHome;

        float dist = Mathf.Abs(markerPos - zoneCenter);
        float remainingBefore = weapon.ReloadTimeRemaining;

        if (dist <= perfectWidth * 0.5f)
        {
            float saved = Apply(Mathf.Lerp(perfectRemainingShaky, perfectRemainingSteady, stability01), remainingBefore);
            Show($"PERFECT  -{saved:0.0}s", perfectColor);
        }
        else if (dist <= goodWidth * 0.5f)
        {
            float saved = Apply(Mathf.Lerp(goodRemainingShaky, goodRemainingSteady, stability01), remainingBefore);
            Show($"GOOD  -{saved:0.0}s", goodColor);
        }
        else
        {
            // a miss costs nothing: the normal reload just carries on
            Show("MISS", missColor);
        }
    }

    // Cuts what is left of the reload to 'remainingFraction' of it. Returns the seconds saved (for the label).
    float Apply(float remainingFraction, float remainingBefore)
    {
        float scale = Mathf.Min(maxSpeedScale, 1f / Mathf.Max(0.02f, remainingFraction));
        weapon.SetReloadSpeedScale(scale);
        return remainingBefore * (1f - 1f / scale);
    }

    void Show(string text, Color color)
    {
        resultLabel.text = text;
        resultLabel.color = color;
        markerImage.color = color;
    }
}
