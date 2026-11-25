using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// The puzzle hint button (cash register, combination lock): after the player has tried and failed once a countdown starts; when it
/// has run out a "Hint" button (or H) on the puzzle's screen makes Khalid say the hint. Solved = no more hint. Off in Options / New
/// Game (HintSettings). Built at runtime, nothing in the scenes.
/// </summary>
public class PuzzleHint : MonoBehaviour
{
    public const float CountdownSeconds = 45f;

    class Entry { public float failedAt = -1f; public bool solved; }
    static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    static PuzzleHint instance;

    string openId;
    string openLine;
    GameObject openPanel;
    CanvasGroup group;
    TMP_Text label;
    Image box;
    Button button;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { entries.Clear(); instance = null; }

    static Entry Get(string id)
    {
        if (!entries.TryGetValue(id, out var e)) entries[id] = e = new Entry();
        return e;
    }

    /// <summary>The player tried it and it was wrong: the countdown starts (only the first time).</summary>
    public static void Failed(string id)
    {
        var e = Get(id);
        if (!e.solved && e.failedAt < 0f) e.failedAt = Time.realtimeSinceStartup;
    }

    public static void Solved(string id)
    {
        Get(id).solved = true;
        if (instance != null && instance.openId == id) instance.Show(false);
    }

    /// <summary>The puzzle's screen is up: the button lives on it as long as 'panel' is active.</summary>
    public static void Opened(string id, string subtitleKey, GameObject panel)
    {
        if (instance == null)
        {
            var go = new GameObject("Puzzle Hint");
            instance = go.AddComponent<PuzzleHint>();
            instance.Build();
        }
        instance.openId = id;
        instance.openLine = subtitleKey;
        instance.openPanel = panel;
    }

    /// <summary>For tests.</summary>
    public static float SecondsLeft(string id)
    {
        var e = Get(id);
        return e.failedAt < 0f ? -1f : Mathf.Max(0f, CountdownSeconds - (Time.realtimeSinceStartup - e.failedAt));
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var rt = new GameObject("Hint Button", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-60f, -60f);
        rt.sizeDelta = new Vector2(230f, 64f);
        box = rt.gameObject.AddComponent<Image>();
        box.color = new Color(0.05f, 0.04f, 0.03f, 0.82f);
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.78f, 0.7f, 0.55f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);
        button = rt.gameObject.AddComponent<Button>();
        var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
        button.onClick.AddListener(Say);
        group = rt.gameObject.AddComponent<CanvasGroup>();

        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(rt, false);
        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = 28f;
        t.color = new Color(0.95f, 0.92f, 0.8f, 1f);
        t.raycastTarget = false;
        label = t;
        Show(false);
    }

    void Show(bool on)
    {
        if (group == null) return;
        group.alpha = on ? 1f : 0f;
        group.blocksRaycasts = group.interactable = on;
    }

    void Update()
    {
        bool panelUp = openPanel != null && openPanel.activeInHierarchy && !string.IsNullOrEmpty(openId);
        var e = panelUp ? Get(openId) : null;
        bool wanted = panelUp && HintSettings.Enabled && !e.solved && e.failedAt >= 0f;
        if (!wanted) { Show(false); return; }

        float left = SecondsLeft(openId);
        bool ready = left <= 0f;
        bool subtitle = SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy();
        Show(!subtitle);
        button.interactable = ready;
        label.text = ready ? "[H]  Hint" : "Hint in 0:" + Mathf.CeilToInt(left).ToString("00");
        label.alpha = ready ? 1f : 0.55f;
        if (ready && !subtitle && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) Say();
    }

    void Say()
    {
        if (string.IsNullOrEmpty(openLine) || SecondsLeft(openId) > 0f) return;
        if (SubtitleManager.instance == null || SubtitleManager.instance.IsSubtitleBusy()) return;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        // a thought, not a pause: it goes by itself (E types on the register's keypad and leaves the lock)
        SubtitleManager.ShowSubtitle(new LocalizedString("Subtitles", openLine), false, null, 3.2f);
    }
}

/// <summary>Hints on / off: asked at New Game and in Options.</summary>
public static class HintSettings
{
    public const string Key = "HintsEnabled";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(Key, 1) == 1;
        set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
