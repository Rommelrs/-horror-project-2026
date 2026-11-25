using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// New Game in the main menu asks once: puzzle hints on or off (can be changed later in Options). Built at runtime in the menu's own
/// look: the buttons are copies of the New Game button.
/// </summary>
public class NewGameOptions : MonoBehaviour
{
    /// <summary>Start was pressed on this panel: the next LoadButton.NewGame really starts.</summary>
    public static bool Confirmed;

    LoadButton caller;
    Text hintsLabel;
    GameObject lastSelected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Confirmed = false; }

    static bool Arabic => OptionsManager.GetLanguageCode() == "ar";
    static string L(string en, string ar) => Arabic ? ArabicSupport.ArabicFixer.Fix(ar, false, false) : en;

    public static void Show(LoadButton caller)
    {
        var template = Template(caller);
        if (template == null) { Confirmed = true; caller.NewGame(); return; }
        var go = new GameObject("New Game Options", typeof(RectTransform));
        go.AddComponent<NewGameOptions>().Build(caller, template);
    }

    static Button Template(LoadButton caller)
    {
        foreach (var k in FindObjectsByType<Button>(FindObjectsSortMode.None))
            for (int i = 0; i < k.onClick.GetPersistentEventCount(); i++)
                if (k.onClick.GetPersistentTarget(i) == caller && k.onClick.GetPersistentMethodName(i) == "NewGame"
                    && k.GetComponentInChildren<Text>(true) != null) return k;
        return null;
    }

    void Build(LoadButton from, Button template)
    {
        caller = from;
        lastSelected = template.gameObject;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var dim = Rect("Dim", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.86f);

        var src = template.GetComponentInChildren<Text>(true);
        Label(src, "Title", L("PUZZLE HINTS", "تلميحات الألغاز"), 190f, 60);
        Label(src, "Text", L("If you are stuck on a puzzle, a hint button appears. You can change this later in Options.", "إذا علقت في لغز سيظهر زر تلميح."), 110f, 32);
        var hints = MakeButton(template, 0f, "", Toggle);
        hintsLabel = hints.GetComponentInChildren<Text>(true);
        var start = MakeButton(template, -110f, L("Start", "ابدأ"), Begin);
        var back = MakeButton(template, -220f, L("Back", "رجوع"), Close);
        Chain(hints, start, back);
        Refresh();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(start.gameObject);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = min; rt.anchorMax = max; rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    void Label(Text like, string name, string text, float y, int size)
    {
        var rt = Rect(name, transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1500f, 100f));
        var t = rt.gameObject.AddComponent<Text>();
        t.font = like.font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = new Color(0.84f, 0.79f, 0.71f);
        t.text = text; t.raycastTarget = false;
    }

    Button MakeButton(Button template, float y, string text, UnityEngine.Events.UnityAction click)
    {
        var go = Instantiate(template.gameObject, transform);
        go.SetActive(true);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
        rt.sizeDelta = new Vector2(Mathf.Max(420f, rt.sizeDelta.x), Mathf.Clamp(rt.sizeDelta.y, 70f, 95f));
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (c is LoadButton || c.GetType().Name.StartsWith("Localize")) DestroyImmediate(c);
        var b = go.GetComponent<Button>();
        b.onClick = new Button.ButtonClickedEvent();
        b.interactable = true;
        b.onClick.AddListener(click);
        var t = go.GetComponentInChildren<Text>(true);
        if (t != null) { t.text = text; t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Overflow; }
        return b;
    }

    static void Chain(params Button[] list)
    {
        for (int i = 0; i < list.Length; i++)
        {
            var n = new Navigation { mode = Navigation.Mode.Explicit };
            n.selectOnUp = list[(i + list.Length - 1) % list.Length];
            n.selectOnDown = list[(i + 1) % list.Length];
            list[i].navigation = n;
        }
    }

    void Refresh() => hintsLabel.text = HintSettings.Enabled ? L("Hints:  ON", "التلميحات: مفعلة") : L("Hints:  OFF", "التلميحات: معطلة");

    void Toggle() { HintSettings.Enabled = !HintSettings.Enabled; Refresh(); }

    void Begin()
    {
        Confirmed = true;
        caller.NewGame();
        Destroy(gameObject);
    }

    void Close()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(lastSelected);
        Destroy(gameObject);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }
}
