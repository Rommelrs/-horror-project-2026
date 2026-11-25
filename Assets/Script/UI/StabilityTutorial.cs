using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// What Stability does: two looping clips side by side (aiming and reloading at full and at no stability). Comes up once - after the first
/// Questioner is killed, or after the cutscene outside the store if he got away from him - and any time from Options. Built at runtime;
/// the clips are Resources/Tutorials/Stability100 and Stability0.
/// </summary>
public class StabilityTutorial : MonoBehaviour
{
    public const string Flag = "StabilityTutorialShown";
    /// <summary>It is on screen: it has the keyboard.</summary>
    public static bool Open { get; private set; }

    static StabilityTutorial waiting;
    bool pausedByUs;
    Text anyKey;
    readonly RenderTexture[] targets = new RenderTexture[2];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Open = false; waiting = null; }

    static bool Arabic => OptionsManager.GetLanguageCode() == "ar";
    static string L(string en, string ar) => Arabic ? ArabicSupport.ArabicFixer.Fix(ar, false, false) : en;

    /// <summary>Once per game: as soon as nothing else is going on, `delay` s from now.</summary>
    public static void ShowOnce(float delay)
    {
        if (StoryFlags.Has(Flag) || waiting != null || Open) return;
        waiting = new GameObject("Stability Tutorial (waiting)").AddComponent<StabilityTutorial>();
        waiting.StartCoroutine(waiting.Co_Wait(delay));
    }

    IEnumerator Co_Wait(float delay)
    {
        yield return new WaitForSeconds(delay);
        while (Busy()) yield return null;
        waiting = null;
        StoryFlags.Set(Flag);
        Show();
        Destroy(gameObject);
    }

    static bool Busy()
    {
        var p = Player.instance;
        if (p == null || p.IsDead() || p.isGrabbed || p.isKnockedDown) return true;
        if (GameManager.IsPaused || CutsceneGate.Playing || FadeScreenUI.ScreenCovered || EnemyFreeze.Active) return true;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return true;
        if (InventoryUI.instance != null && InventoryUI.instance.InventoryIsActive()) return true;
        if (ItemInspectionHandler.instance != null && ItemInspectionHandler.instance.InspectionMenuIsActive()) return true;
        if (MapHandler.instance != null && MapHandler.instance.MapIsActive()) return true;
        return LevelManager.instance != null && (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon);
    }

    public static void Show()
    {
        if (Open) return;
        Open = true;
        var s = new GameObject("Stability Tutorial", typeof(RectTransform)).AddComponent<StabilityTutorial>();
        s.Build();
        s.StartCoroutine(s.Co_Run());
    }

    // ---------------------------------------------------------------- look

    static Font GameFont()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Text>())
            if (t.font != null && t.font.name == "AppleGaramond") return t.font;
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    Text Label(string text, Vector2 pos, Vector2 size, int fontSize, Color color)
    {
        var t = Rect("Text", transform, pos, size).gameObject.AddComponent<Text>();
        t.font = font; t.fontSize = fontSize; t.color = color; t.text = text; t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
        return t;
    }

    Font font;

    void Build()
    {
        font = GameFont();
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var back = Rect("Back", transform, Vector2.zero, Vector2.zero);
        back.anchorMin = Vector2.zero; back.anchorMax = Vector2.one; back.sizeDelta = new Vector2(8f, 8f);
        back.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.015f, 0.01f, 0.97f);

        var sand = new Color(0.84f, 0.79f, 0.71f);
        Label(L("STABILITY", "الاتزان"), new Vector2(0f, 455f), new Vector2(1400f, 80f), 64, sand);
        Panel(0, -420f, "100%", new Color(0.62f, 0.75f, 0.55f), L("Precise aiming and efficient handling", "تصويب دقيق وتحكم فعال"));
        Panel(1, 420f, "0%", new Color(0.8f, 0.28f, 0.22f), L("Unsteady aiming and impaired weapon handling", "تصويب مهتز وتحكم ضعيف بالسلاح"));
        Label(L("Stability represents Khalid's mental state. As it decreases, controlling weapons becomes increasingly difficult.\nMaintain your Stability to remain effective in combat.",
            "يمثل الاتزان حالة خالد النفسية. كلما انخفض أصبح التحكم بالأسلحة أصعب. حافظ على اتزانك لتبقى فعالاً في القتال."), new Vector2(0f, -325f), new Vector2(1600f, 130f), 36, sand);
        anyKey = Label(L("Press any key to continue", "اضغط أي زر للمتابعة"), new Vector2(0f, -455f), new Vector2(1000f, 60f), 32, sand);
    }

    void Panel(int index, float x, string percent, Color tint, string caption)
    {
        Label(percent, new Vector2(x, 365f), new Vector2(760f, 70f), 58, tint);
        Rect("Frame", transform, new Vector2(x, 90f), new Vector2(776f, 443f)).gameObject.AddComponent<Image>().color = new Color(tint.r, tint.g, tint.b, 0.55f);
        var raw = Rect("Video", transform, new Vector2(x, 90f), new Vector2(768f, 432f)).gameObject.AddComponent<RawImage>();
        raw.color = Color.black;
        var clip = Resources.Load<VideoClip>("Tutorials/" + (index == 0 ? "Stability100" : "Stability0"));
        if (clip != null)
        {
            var rtx = new RenderTexture(768, 432, 0) { name = "StabilityTutorial" };
            targets[index] = rtx;
            var v = raw.gameObject.AddComponent<VideoPlayer>();
            v.playOnAwake = false; v.source = VideoSource.VideoClip; v.clip = clip; v.isLooping = true;
            v.audioOutputMode = VideoAudioOutputMode.None; v.renderMode = VideoRenderMode.RenderTexture; v.targetTexture = rtx;
            v.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            v.prepareCompleted += p => { raw.texture = rtx; raw.color = Color.white; p.Play(); };
            v.Prepare();
        }
        Label(caption, new Vector2(x, -175f), new Vector2(800f, 60f), 34, new Color(0.84f, 0.79f, 0.71f));
    }

    IEnumerator Co_Run()
    {
        if (!GameManager.IsPaused) { GameManager.IsPaused = true; pausedByUs = true; }
        var group = gameObject.AddComponent<CanvasGroup>();
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f) { group.alpha = t; yield return null; }
        group.alpha = 1f;
        anyKey.enabled = false;
        yield return new WaitForSecondsRealtime(1.2f);        // not skipped by a key that was still being mashed
        anyKey.enabled = true;
        while (!AnyPress())
        {
            var c = anyKey.color; c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.5f); anyKey.color = c;
            yield return null;
        }
        // the key that closes it does nothing else
        float until = Time.unscaledTime + 0.4f;
        yield return null;
        while (Time.unscaledTime < until && AnyHeld()) yield return null;
        Close();
    }

    static bool AnyPress()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
        var g = Gamepad.current;
        return g != null && (g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame || g.startButton.wasPressedThisFrame);
    }

    static bool AnyHeld()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;
        return Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed);
    }

    void Close()
    {
        if (pausedByUs) { pausedByUs = false; GameManager.IsPaused = false; }
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (waiting == this) { waiting = null; return; }
        if (pausedByUs) GameManager.IsPaused = false;
        foreach (var t in targets) if (t != null) { t.Release(); Destroy(t); }
        if (anyKey != null) Open = false;
    }
}
