using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// PS1-style healing items banner (Painkiller / Med Kit).
// Slides in from the left when one is picked up - the picked row sits on a sandy band and its count ticks up - and
// again as a reminder when the player drops to low health (LowHealthEffect calls ShowReminder).
// The banner is drawn by its own camera into a low-res render texture (pixelHeight tall) that this RawImage stretches
// over the screen with point filtering, so the text, icons and band all come out as chunky pixels. Everything inside
// is built at runtime from the settings below. Built by Tools > Items > Build Healing Items (PS1).
[RequireComponent(typeof(RawImage))]
public class HealingItemsHUD : MonoBehaviour
{
    public static HealingItemsHUD instance;

    [System.Serializable]
    public class Entry
    {
        public Item item;
        public string label;   // shown instead of item.itemName (the inspection objects are matched by itemName)
        public Sprite icon;    // falls back to item.itemIcon
    }

    [SerializeField] Entry[] entries;

    [Header("Look")]
    [Tooltip("Render texture height in pixels - lower = chunkier AND a bigger banner (the layout below is in these pixels).")]
    [SerializeField] int pixelHeight = 720;
    [SerializeField] Font font;
    [SerializeField] int fontSize = 17;
    [SerializeField] Texture2D sandBand;
    [SerializeField] Color textColor = new Color(0.93f, 0.89f, 0.78f, 1f);
    [SerializeField] Color dimTextColor = new Color(0.8f, 0.76f, 0.67f, 1f);
    [SerializeField] Color countFlashColor = new Color(1f, 0.97f, 0.85f, 1f);
    [SerializeField] Color lineColor = new Color(0.42f, 0.33f, 0.2f, 0.9f);
    [SerializeField] Color backdropColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Layout (render texture pixels)")]
    [SerializeField] Vector2 position = new Vector2(4f, 20f);   // panel top-left, from the left edge / screen middle
    [SerializeField] float width = 258f;
    [SerializeField] float rowHeight = 36f;
    [SerializeField] float iconSize = 32f;

    [Header("Timing")]
    [SerializeField] float slideInTime = 0.45f;
    [SerializeField] float holdTime = 2.8f;
    [SerializeField] float slideOutTime = 0.4f;
    [SerializeField] float countDelay = 0.15f;
    [SerializeField] float countStep = 0.2f;
    [SerializeField] float countPopTime = 0.3f;

    const int UILayer = 5;

    class Row
    {
        public Entry entry;
        public RectTransform rect;
        public RawImage band;
        public Image icon;
        public Text name;
        public Text count;
        public int shown;      // number on screen
        public int target;
        public float popT = -1f;
    }

    enum State { Hidden, In, Hold, Out }

    RawImage display;
    Camera pixelCamera;
    RenderTexture rt;
    RectTransform panel;
    Image backdrop;
    Image line;
    readonly List<Row> rows = new List<Row>();

    State state = State.Hidden;
    float stateT;
    bool pending;
    Item highlight;
    float nextCountTick;

    void Awake()
    {
        instance = this;
        display = GetComponent<RawImage>();
        display.raycastTarget = false;
        display.enabled = false;
        Build();
    }

    void OnEnable() => Inventory.OnItemPickedUp += OnItemPickedUp;
    void OnDisable() => Inventory.OnItemPickedUp -= OnItemPickedUp;

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (pixelCamera != null) Destroy(pixelCamera.gameObject);
        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    // ---------- public ----------

    // low-health reminder: what the player has to heal with (nothing to show = no banner)
    public void ShowReminder()
    {
        if (!Prepare(null, 0, 0)) return;
        Request();
    }

    // ---------- events ----------

    void OnItemPickedUp(Item item, int before, int after) => ShowCount(item, before, after);

    // highlights one item and counts its number from 'before' to 'after' (up for a pickup, down for a use)
    public void ShowCount(Item item, int before, int after)
    {
        if (FindRow(item) == null) return;
        if (!Prepare(item, before, after)) return;
        Request();
    }

    // sets which rows show and the numbers they count from / to
    bool Prepare(Item picked, int before, int after)
    {
        var inventory = Player.instance != null ? Player.instance.inventory : null;
        if (inventory == null) return false;

        bool visibleNow = state != State.Hidden;
        highlight = picked;
        int any = 0;
        foreach (var row in rows)
        {
            int count = inventory.CountOf(row.entry.item);
            bool show = count > 0 || row.entry.item == picked;
            row.rect.gameObject.SetActive(show);
            if (!show) continue;
            any++;

            if (row.entry.item == picked)
            {
                // a second pickup while it's still on screen counts on from what is shown
                row.shown = visibleNow ? Mathf.Min(row.shown, before) : before;
                row.target = after;
            }
            else
                row.shown = row.target = count;

            row.count.text = row.shown.ToString();
        }

        if (any == 0) return false;
        Layout();
        return true;
    }

    void Request()
    {
        // already up: keep it there longer (counting starts once it has slid in)
        if (state == State.Hold) { stateT = 0f; nextCountTick = Time.unscaledTime + countDelay; return; }
        if (state == State.In) return;
        pending = true;
    }

    // ---------- update ----------

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (pending && CanShow())
        {
            pending = false;
            // coming back from sliding out: continue from where it is
            float from = state == State.Out ? 1f - Mathf.Clamp01(stateT / slideOutTime) : 0f;
            SetState(State.In);
            stateT = from * slideInTime;
        }

        if (state == State.Hidden) return;
        stateT += dt;

        float k;   // 0 = off screen, 1 = in place
        switch (state)
        {
            case State.In:
                k = EaseOutCubic(Mathf.Clamp01(stateT / slideInTime));
                if (stateT >= slideInTime) { SetState(State.Hold); nextCountTick = Time.unscaledTime + countDelay; }
                break;
            case State.Hold:
                k = 1f;
                if (stateT >= holdTime && !Counting()) SetState(State.Out);
                break;
            default:
                k = 1f - EaseInCubic(Mathf.Clamp01(stateT / slideOutTime));
                if (stateT >= slideOutTime) { SetState(State.Hidden); k = 0f; }
                break;
        }

        // slide in whole render texture pixels, chunky like the rest of it
        float offX = -(width + position.x + 8f);
        panel.anchoredPosition = new Vector2(Mathf.Round(Mathf.Lerp(offX, position.x, k)), position.y);
        display.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 1.6f));

        if (state == State.Hold) TickCounts();
        UpdatePops(dt);
    }

    bool CanShow()
    {
        if (GameManager.IsPaused || Time.timeScale <= 0f) return false;   // wait for the inspection / subtitle to close
        var player = Player.instance;
        return player != null && !player.IsDead();
    }

    void SetState(State s)
    {
        state = s;
        stateT = 0f;
        bool on = s != State.Hidden;
        display.enabled = on;
        if (pixelCamera != null) pixelCamera.enabled = on;
        if (on) FitTexture();
    }

    bool Counting()
    {
        foreach (var row in rows)
            if (row.rect.gameObject.activeSelf && (row.shown != row.target || row.popT >= 0f)) return true;
        return false;
    }

    void TickCounts()
    {
        if (Time.unscaledTime < nextCountTick) return;
        foreach (var row in rows)
        {
            if (!row.rect.gameObject.activeSelf || row.shown == row.target) continue;
            row.shown += row.target > row.shown ? 1 : -1;
            row.count.text = row.shown.ToString();
            row.popT = 0f;
            nextCountTick = Time.unscaledTime + countStep;
            stateT = Mathf.Min(stateT, holdTime * 0.5f);   // let the new number sit for a moment
        }
    }

    void UpdatePops(float dt)
    {
        foreach (var row in rows)
        {
            if (row.popT < 0f) continue;
            row.popT += dt / Mathf.Max(0.01f, countPopTime);
            float t = Mathf.Clamp01(row.popT);
            float s = Mathf.Lerp(1.5f, 1f, EaseOutCubic(t));
            row.count.rectTransform.localScale = new Vector3(s, s, 1f);
            row.count.color = Color.Lerp(countFlashColor, textColor, t);
            if (row.popT >= 1f) row.popT = -1f;
        }
    }

    static float EaseOutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
    static float EaseInCubic(float t) => t * t * t;

    Row FindRow(Item item)
    {
        foreach (var row in rows)
            if (row.entry.item == item) return row;
        return null;
    }

    // ---------- building ----------

    void Build()
    {
        var camGO = new GameObject("HealingItemsHUD Camera");
        camGO.hideFlags = HideFlags.DontSave;
        camGO.transform.position = new Vector3(0f, -10000f, 0f);
        pixelCamera = camGO.AddComponent<Camera>();
        pixelCamera.clearFlags = CameraClearFlags.SolidColor;
        pixelCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        pixelCamera.cullingMask = 1 << UILayer;
        pixelCamera.orthographic = true;
        pixelCamera.nearClipPlane = 0.1f;
        pixelCamera.farClipPlane = 10f;
        pixelCamera.depth = -100;
        pixelCamera.allowHDR = false;
        pixelCamera.allowMSAA = false;
        pixelCamera.enabled = false;
        FitTexture();

        var canvasGO = new GameObject("Canvas", typeof(RectTransform));
        canvasGO.layer = UILayer;
        canvasGO.transform.SetParent(camGO.transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = pixelCamera;
        canvas.planeDistance = 1f;
        canvas.pixelPerfect = true;
        // authored in render texture pixels
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        panel = NewRect("Panel", canvasGO.transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 0.5f);
        panel.pivot = new Vector2(0f, 1f);
        panel.sizeDelta = new Vector2(width, rowHeight);

        // dark backdrop so it reads over bright places, fading out to the right
        backdrop = NewRect("Backdrop", panel).gameObject.AddComponent<Image>();
        backdrop.sprite = Sprite.Create(GradientTexture(), new Rect(0, 0, 64, 32), new Vector2(0.5f, 0.5f));
        backdrop.color = backdropColor;
        backdrop.raycastTarget = false;
        var br = backdrop.rectTransform;
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(-position.x - 4f, -22f); br.offsetMax = new Vector2(40f, 18f);

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null || entries[i].item == null) continue;
            rows.Add(BuildRow(entries[i]));
        }

        line = NewRect("Line", panel).gameObject.AddComponent<Image>();
        line.color = lineColor;
        line.raycastTarget = false;
    }

    Row BuildRow(Entry e)
    {
        var row = new Row { entry = e };
        row.rect = NewRect(e.label, panel);
        row.rect.anchorMin = row.rect.anchorMax = new Vector2(0f, 1f);
        row.rect.pivot = new Vector2(0f, 1f);
        row.rect.sizeDelta = new Vector2(width, rowHeight);

        row.band = NewRect("Band", row.rect).gameObject.AddComponent<RawImage>();
        row.band.texture = sandBand;
        row.band.raycastTarget = false;
        var b = row.band.rectTransform;
        b.anchorMin = Vector2.zero; b.anchorMax = Vector2.one; b.offsetMin = b.offsetMax = Vector2.zero;

        row.icon = NewRect("Icon", row.rect).gameObject.AddComponent<Image>();
        row.icon.sprite = e.icon != null ? e.icon : e.item.itemIcon;
        row.icon.preserveAspect = true;
        row.icon.raycastTarget = false;
        var ir = row.icon.rectTransform;
        ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
        ir.sizeDelta = new Vector2(iconSize, iconSize);
        ir.anchoredPosition = new Vector2(Mathf.Round(18f + iconSize * 0.5f), 0f);

        row.name = NewText("Name", row.rect, string.IsNullOrEmpty(e.label) ? e.item.itemName : e.label, TextAnchor.MiddleLeft);
        var nr = row.name.rectTransform;
        nr.anchorMin = new Vector2(0f, 0f); nr.anchorMax = new Vector2(1f, 1f);
        nr.offsetMin = new Vector2(iconSize + 30f, 0f); nr.offsetMax = new Vector2(-40f, 0f);

        row.count = NewText("Count", row.rect, "0", TextAnchor.MiddleCenter);
        var cr = row.count.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(1f, 0.5f);
        cr.sizeDelta = new Vector2(30f, rowHeight);
        cr.anchoredPosition = new Vector2(-30f, 0f);
        return row;
    }

    void Layout()
    {
        float y = 0f;
        foreach (var row in rows)
        {
            if (!row.rect.gameObject.activeSelf) continue;
            row.rect.anchoredPosition = new Vector2(0f, -y);
            bool lit = row.entry.item == highlight;
            row.band.enabled = lit;
            row.name.color = lit || highlight == null ? textColor : dimTextColor;
            row.count.color = row.name.color;
            row.count.rectTransform.localScale = Vector3.one;
            row.popT = -1f;
            y += rowHeight;
        }
        panel.sizeDelta = new Vector2(width, y);

        var lr = line.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0f, 1f);
        lr.pivot = new Vector2(0f, 1f);
        lr.sizeDelta = new Vector2(width - 12f, 1f);
        lr.anchoredPosition = new Vector2(10f, -y - 2f);
    }

    // keeps the render texture pixelHeight tall at the screen's aspect
    void FitTexture()
    {
        int h = Mathf.Max(90, pixelHeight);
        int w = Mathf.Max(1, Mathf.RoundToInt(h * (float)Screen.width / Mathf.Max(1, Screen.height)));
        if (rt != null && rt.width == w && rt.height == h) return;

        if (rt != null) { pixelCamera.targetTexture = null; rt.Release(); Destroy(rt); }
        rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "HealingItemsHUD RT" };
        rt.Create();
        pixelCamera.targetTexture = rt;
        if (display != null) display.texture = rt;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UILayer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    Text NewText(string name, Transform parent, string text, TextAnchor anchor)
    {
        var t = NewRect(name, parent).gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = textColor;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = t.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    static Texture2D GradientTexture()
    {
        // fades out to the right and softly at the top and bottom
        var tex = new Texture2D(64, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 64; x++)
            {
                float h = 1f - Mathf.Clamp01((x / 63f - 0.45f) / 0.55f);
                float v = Mathf.Clamp01(Mathf.Min(y, 31 - y) / 9f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, h * h * (3f - 2f * h) * v * v * (3f - 2f * v)));
            }
        tex.Apply();
        return tex;
    }
}
