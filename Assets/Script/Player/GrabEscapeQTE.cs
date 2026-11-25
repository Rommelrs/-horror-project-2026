using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The Questioner grab escape (user 2026-11-20): two quick sliders while he holds Khalid. A marker runs once across a bar;
/// press Interact while it is inside the lit zone. Pass the first and the second comes at once; pass both = Khalid shoves him
/// off before the bite. Pressing outside the zone, or letting the marker run out, fails it - the grab goes on to the bite.
/// The lower Khalid's stability, the faster the marker runs; and every time he gets out of the same Questioner's grab, faster still (failing doesn't change it). Built at runtime by PlayerCloseCombat (BeginGrab / EndGrab).
/// </summary>
public class GrabEscapeQTE : MonoBehaviour
{
    public enum Result { None, Running, Escaped, Failed }
    public Result State { get; private set; }
    /// <summary>Read-only, for tests: the marker (0..1), the zone, and whether the marker is moving yet.</summary>
    public float Marker => pos;
    public float ZoneStart => zoneStart;
    public float ZoneWidth => zoneWidth;
    public bool MarkerMoving => running && wait <= 0f;
    public int Slider => slider;
    public float Sweep => sweep;

    [Header("Speed (one pass of the marker, s)")]
    [Tooltip("At full stability.")]
    public float sweepFullStability = 0.95f;
    [Tooltip("At no stability: much faster.")]
    public float sweepNoStability = 0.42f;
    [Tooltip("The second slider runs this much faster than the first (x).")]
    public float secondSpeedUp = 1.1f;
    [Tooltip("Each earlier ESCAPE from the SAME Questioner makes both sliders this much faster (+x per escape). A failed try doesn't.")]
    public float repeatSpeedUp = 0.2f;
    [Tooltip("...up to this many times as fast.")]
    public float maxRepeatSpeedUp = 1.8f;
    [Tooltip("Never quicker than this per pass (s), however low the stability and however often he was caught.")]
    public float minSweep = 0.28f;

    [Header("Zone (fraction of the bar)")]
    public float zoneWidthFirst = 0.2f;
    public float zoneWidthSecond = 0.16f;

    [Header("Rhythm (s)")]
    [Tooltip("The bar shows this long before the first marker starts (he has just been caught).")]
    public float startDelay = 0.3f;
    [Tooltip("Between the first slider and the second.")]
    public float betweenSliders = 0.18f;

    static readonly Color BarColor = new Color(0.05f, 0.04f, 0.03f, 0.75f);
    static readonly Color FrameColor = new Color(0.78f, 0.7f, 0.55f, 0.55f);
    static readonly Color ZoneColor = new Color(0.86f, 0.55f, 0.2f, 0.95f);
    static readonly Color PassColor = new Color(0.95f, 0.92f, 0.8f, 1f);
    static readonly Color FailColor = new Color(0.75f, 0.08f, 0.05f, 1f);
    static readonly Color PipOff = new Color(0.2f, 0.18f, 0.15f, 0.9f);

    CanvasGroup group;
    RectTransform zone, marker;
    Image zoneImg, markerImg;
    Image[] pips;
    TMP_Text keyText;

    int slider;              // 0 = first, 1 = second
    float pos, sweep, zoneStart, zoneWidth, wait, hideAt = -1f, fade;
    bool running;
    float repeatMul = 1f;
    /// <summary>Read-only, for tests: how much faster this grab's sliders run for the repeat catches (x).</summary>
    public float RepeatSpeedUp => repeatMul;

    public static GrabEscapeQTE Create(Transform owner)
    {
        var go = new GameObject("Grab Escape QTE");
        go.transform.SetParent(owner, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var qte = go.AddComponent<GrabEscapeQTE>();
        qte.Build(go.transform);
        return qte;
    }

    void Build(Transform root)
    {
        var panel = NewRect("Panel", root, Vector2.zero, new Vector2(700f, 110f));
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 330f);
        group = panel.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // the key to press, in a little dark box left of the bar
        NewImage("Key Frame", panel, new Vector2(-305f, 0f), new Vector2(58f, 58f), FrameColor);
        var keyBox = NewImage("Key", panel, new Vector2(-305f, 0f), new Vector2(54f, 54f), BarColor);
        var kt = new GameObject("Key Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        kt.transform.SetParent(keyBox, false);
        kt.rectTransform.sizeDelta = new Vector2(54f, 54f);
        kt.alignment = TextAlignmentOptions.Center;
        kt.fontSize = 30f;
        kt.color = PassColor;
        kt.raycastTarget = false;
        keyText = kt;

        // the bar: a light frame, the dark track, the zone, the marker
        NewImage("Frame", panel, new Vector2(35f, 0f), new Vector2(574f, 34f), FrameColor);
        var bar = NewImage("Bar", panel, new Vector2(35f, 0f), new Vector2(570f, 30f), BarColor);
        zoneImg = NewImage("Zone", bar, Vector2.zero, Vector2.zero, ZoneColor).GetComponent<Image>();
        zone = zoneImg.rectTransform;
        zone.anchorMin = new Vector2(0.5f, 0f); zone.anchorMax = new Vector2(0.7f, 1f);
        zone.offsetMin = zone.offsetMax = Vector2.zero;
        markerImg = NewImage("Marker", bar, Vector2.zero, new Vector2(7f, 46f), PassColor).GetComponent<Image>();
        marker = markerImg.rectTransform;
        marker.anchorMin = marker.anchorMax = new Vector2(0f, 0.5f);

        // two pips: the sliders passed
        pips = new Image[2];
        for (int i = 0; i < 2; i++)
            pips[i] = NewImage("Pip " + (i + 1), panel, new Vector2(300f + i * 22f, 36f), new Vector2(14f, 14f), PipOff).GetComponent<Image>();
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 at, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = at;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform NewImage(string name, Transform parent, Vector2 at, Vector2 size, Color color)
    {
        var rt = NewRect(name, parent, at, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    static InputAction Action => InteractionHandler.InteractAction;

    public const string LessonFlag = "GrabEscapeTutorialDone";
    /// <summary>The very first time: the marker stops in the zone and waits for the key, which flashes.</summary>
    public bool Lesson { get; private set; }
    bool waitingForKey;
    Image keyBox;

    /// <summary>He has just been caught: the bar shows and the first marker starts after startDelay.</summary>
    public void Begin(int earlierEscapes = 0)
    {
        repeatMul = Mathf.Min(maxRepeatSpeedUp, 1f + repeatSpeedUp * Mathf.Max(0, earlierEscapes));
        State = Result.Running;
        running = true;
        Lesson = !StoryFlags.Has(LessonFlag);
        waitingForKey = false;
        slider = 0;
        foreach (var p in pips) p.color = PipOff;
        var a = Action;
        string key = a != null ? a.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions) : "E";
        keyText.text = string.IsNullOrEmpty(key) ? "E" : key;
        hideAt = -1f;
        StartSlider(startDelay);
    }

    /// <summary>The grab ended some other way (he was killed, blasted...): gone, nothing decided.</summary>
    public void Cancel()
    {
        if (State == Result.Running) State = Result.None;
        running = false;
        FlashKey(false);
        if (hideAt < 0f) hideAt = Time.unscaledTime;
    }

    void StartSlider(float delay)
    {
        float stab = 1f;
        var p = Player.instance;
        if (p != null && p.playerStability != null && p.playerStability.maxStability > 0)
            stab = Mathf.Clamp01(p.playerStability.stability / (float)p.playerStability.maxStability);
        sweep = Mathf.Lerp(sweepNoStability, sweepFullStability, stab) / (slider == 1 ? secondSpeedUp : 1f) / repeatMul;
        sweep = Mathf.Max(minSweep, sweep);
        zoneWidth = slider == 0 ? zoneWidthFirst : zoneWidthSecond;
        // never right at the start (no time to react), never touching the end
        zoneStart = slider == 0 ? Random.Range(0.4f, 0.88f - zoneWidth) : Random.Range(0.3f, 0.9f - zoneWidth);
        zone.anchorMin = new Vector2(zoneStart, 0f);
        zone.anchorMax = new Vector2(zoneStart + zoneWidth, 1f);
        zone.offsetMin = zone.offsetMax = Vector2.zero;
        zoneImg.color = ZoneColor;
        markerImg.color = PassColor;
        pos = 0f;
        wait = delay;
        PlaceMarker();
    }

    void PlaceMarker()
    {
        marker.anchorMin = marker.anchorMax = new Vector2(pos, 0.5f);
        marker.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        // fade in while it runs, out after it's decided
        float target = running || (hideAt >= 0f && Time.unscaledTime < hideAt) ? 1f : 0f;
        fade = Mathf.MoveTowards(fade, target, Time.unscaledDeltaTime / 0.12f);
        group.alpha = fade;
        if (!running) return;

        float dt = Time.deltaTime;          // a pause (or a hit-stop) holds it
        if (dt <= 0f) return;
        if (wait > 0f) { wait -= dt; return; }

        // first time ever: the marker stops in the middle of the zone and the key flashes until he presses it
        if (Lesson && pos + dt / Mathf.Max(0.1f, sweep) >= zoneStart + zoneWidth * 0.5f)
        {
            pos = zoneStart + zoneWidth * 0.5f;
            waitingForKey = true;
            dt = 0f;
        }
        FlashKey(waitingForKey);
        pos += dt / Mathf.Max(0.1f, sweep);
        var a = Action;
        bool pressed = a != null && a.WasPressedThisFrame();
        if (pressed)
        {
            if (pos >= zoneStart && pos <= zoneStart + zoneWidth) Pass();
            else if (!Lesson) Fail();
        }
        else if (pos >= 1f) { pos = 1f; Fail(); }
        PlaceMarker();
    }

    void FlashKey(bool on)
    {
        if (keyBox == null && keyText != null) keyBox = keyText.transform.parent.GetComponent<Image>();
        bool lit = on && Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.28f;
        if (keyBox != null) keyBox.color = lit ? ZoneColor : BarColor;
        if (keyText != null)
        {
            keyText.color = lit ? Color.black : PassColor;
            keyText.transform.parent.localScale = Vector3.one * (lit ? 1.18f : 1f);
        }
    }

    void Pass()
    {
        waitingForKey = false;
        FlashKey(false);
        pips[slider].color = PassColor;
        zoneImg.color = PassColor;
        if (slider == 0)
        {
            slider = 1;
            StartSlider(betweenSliders);
            return;
        }
        if (Lesson) StoryFlags.Set(LessonFlag);
        Lesson = false;
        State = Result.Escaped;
        running = false;
        hideAt = Time.unscaledTime + 0.35f;
    }

    void Fail()
    {
        zoneImg.color = FailColor;
        markerImg.color = FailColor;
        State = Result.Failed;
        running = false;
        hideAt = Time.unscaledTime + 0.45f;
    }
}
