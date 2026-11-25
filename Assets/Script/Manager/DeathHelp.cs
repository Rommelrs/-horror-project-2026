using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Died six times at the same checkpoint with a gun in his pocket: after Continue the game offers to make it easier - 15 extra rounds,
/// now and after every further Continue at that checkpoint. Says no = not asked again until six more deaths there.
/// </summary>
public class DeathHelp : MonoBehaviour
{
    public const int DeathsNeeded = 6, Rounds = 15;
    const string KeyId = "DeathHelp.checkpoint", KeyCount = "DeathHelp.deaths", KeyYes = "DeathHelp.accepted";

    static DeathHelp instance;
    Health tracked;
    bool afterDeath;
    float settle;
    GameObject panel;
    Button yes;
    bool pausedByUs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("Death Help");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DeathHelp>();
    }

    public static bool Open => instance != null && instance.panel != null;
    /// <summary>A new game starts with a clean count.</summary>
    public static void Clear() { PlayerPrefs.DeleteKey(KeyId); PlayerPrefs.DeleteKey(KeyCount); PlayerPrefs.DeleteKey(KeyYes); }

#if UNITY_EDITOR
    /// <summary>TaskJump: five deaths already counted at the checkpoint just made.</summary>
    public static void DebugPrime() { if (instance != null) instance.StartCoroutine(instance.Co_Prime()); }
    System.Collections.IEnumerator Co_Prime()
    {
        yield return new WaitForSecondsRealtime(0.8f);
        PlayerPrefs.SetString(KeyId, CheckpointId()); PlayerPrefs.SetInt(KeyCount, DeathsNeeded - 1); PlayerPrefs.SetInt(KeyYes, 0);
    }
#endif

    static string CheckpointId()
    {
        var d = CheckpointManager.instance != null ? CheckpointManager.instance.PeekCheckpoint() : null;
        return d == null ? "" : d.sceneName + "|" + d.checkpointName + "|" + Mathf.RoundToInt(d.posX) + "," + Mathf.RoundToInt(d.posZ);
    }

    void OnDied()
    {
        string id = CheckpointId();
        if (id == "") return;
        bool same = PlayerPrefs.GetString(KeyId, "") == id;
        if (!same) { PlayerPrefs.SetString(KeyId, id); PlayerPrefs.SetInt(KeyYes, 0); }
        PlayerPrefs.SetInt(KeyCount, same ? PlayerPrefs.GetInt(KeyCount, 0) + 1 : 1);
        afterDeath = true;
    }

    void Update()
    {
        var p = Player.instance;
        if (p != null && p.health != null && p.health != tracked)
        {
            tracked = p.health;
            tracked.OnDeath.AddListener(OnDied);
            settle = 0f;
        }
        if (panel != null) { if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Answer(false); return; }
        if (!afterDeath || p == null || p.IsDead()) return;
        if (LoadingHandler.IsLoading() || CheckpointManager.IsLoadingCheckpoint || GameManager.IsPaused || CutsceneGate.Playing || FadeScreenUI.ScreenCovered) { settle = 0f; return; }
        settle += Time.unscaledDeltaTime;
        if (settle < 1.2f) return;
        afterDeath = false;
        if (PlayerPrefs.GetString(KeyId, "") != CheckpointId() || p.inventory == null || !p.inventory.HasWeapon()) return;
        if (PlayerPrefs.GetInt(KeyYes, 0) == 1) { Give(p); return; }
        if (PlayerPrefs.GetInt(KeyCount, 0) >= DeathsNeeded) Show();
    }

    static void Give(Player p)
    {
        foreach (var it in Resources.LoadAll<Item>("Items"))
            if (it.name == "Light Ammo") { p.inventory.PickUp(it, Rounds); return; }
    }

    static string L(string en, string ar) => OptionsManager.GetLanguageCode() == "ar" ? ArabicSupport.ArabicFixer.Fix(ar, false, false) : en;

    void Show()
    {
        Font font = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Text>()) if (t.font != null && t.font.name == "AppleGaramond") { font = t.font; break; }
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        panel = new GameObject("Death Help Panel", typeof(RectTransform));
        var canvas = panel.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 940;
        var sc = panel.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f); sc.matchWidthOrHeight = 0.5f;
        panel.AddComponent<GraphicRaycaster>();
        var back = Rect(panel.transform, Vector2.zero, Vector2.zero); back.anchorMin = Vector2.zero; back.anchorMax = Vector2.one;
        back.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.86f);
        Label(font, L("This is a hard one.", "هذا الجزء صعب."), new Vector2(0f, 150f), 56);
        Label(font, L("Make it easier? You get 15 extra bullets here.", "هل تريد تسهيله؟ ستحصل على 15 رصاصة إضافية هنا."), new Vector2(0f, 60f), 38);
        yes = MakeButton(font, L("Yes", "نعم"), -60f, () => Answer(true));
        var no = MakeButton(font, L("No", "لا"), -160f, () => Answer(false));
        var n1 = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = no, selectOnUp = no }; yes.navigation = n1;
        var n2 = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = yes, selectOnUp = yes }; no.navigation = n2;
        GameManager.IsPaused = true; pausedByUs = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(yes.gameObject);
    }

    static RectTransform Rect(Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject("UI", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    Text Label(Font font, string s, Vector2 pos, int size, Transform parent = null)
    {
        var t = Rect(parent != null ? parent : panel.transform, pos, new Vector2(1400f, 90f)).gameObject.AddComponent<Text>();
        t.font = font; t.fontSize = size; t.text = s; t.alignment = TextAnchor.MiddleCenter; t.color = new Color(0.84f, 0.79f, 0.71f); t.raycastTarget = false;
        return t;
    }

    Button MakeButton(Font font, string s, float y, UnityEngine.Events.UnityAction click)
    {
        var rt = Rect(panel.transform, new Vector2(0f, y), new Vector2(360f, 78f));
        var im = rt.gameObject.AddComponent<Image>(); im.color = Color.white;
        var b = rt.gameObject.AddComponent<Button>();
        var c = b.colors; c.normalColor = new Color(0.16f, 0.14f, 0.12f, 0.9f); c.highlightedColor = c.selectedColor = new Color(0.42f, 0.35f, 0.26f, 1f); c.pressedColor = new Color(0.6f, 0.5f, 0.36f, 1f); b.colors = c;
        b.onClick.AddListener(click);
        Label(font, s, Vector2.zero, 40, rt);
        return b;
    }

    void Answer(bool ok)
    {
        if (panel == null) return;
        Destroy(panel); panel = null;
        if (pausedByUs) { pausedByUs = false; GameManager.IsPaused = false; }
        PlayerPrefs.SetInt(KeyCount, 0);
        if (!ok || Player.instance == null) return;
        PlayerPrefs.SetInt(KeyYes, 1);
        Give(Player.instance);
    }
}
