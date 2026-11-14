using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Options > Controls: click a row, press a key or mouse button, and that control moves there (Esc cancels). A key already
/// used by another row swaps with it. Changed rows show the new key on a blank keycap; "Restore Defaults" (with an "Are you
/// sure?" box) puts everything back. Keyboard & mouse only; the mouse wheel weapon switch and Pause (Esc) stay fixed.
/// Lives on ControlsGroup in OptionsManager.prefab; the rows are found by name, the extra UI is built here at runtime.
/// </summary>
public class ControlsRebindUI : MonoBehaviour
{
    [SerializeField] InputActionAsset inputAsset;
    [Tooltip("Blank keycap (9-sliced) the new key is written on.")]
    [SerializeField] Sprite blankKey;
    [SerializeField] Color keyTextColor = new Color(0.93f, 0.89f, 0.84f, 1f);
    [Tooltip("Keycap pictures for every key (Assets/UI/ControlIcons/Keys, key_<UTF-16 codes>), same style as the original icons.")]
    [SerializeField] Sprite[] keySprites;
    [SerializeField] Sprite leftMouse, rightMouse, middleMouse;
    Dictionary<string, Sprite> keyLookup;

    class Spec
    {
        public string row, action, mirror;    // mirror: a second action that always follows (Fire -> LightAttack)
        public bool composite;                // Move: the four WASD parts
    }

    static readonly Spec[] specs =
    {
        new Spec { row = "KeybindRow_Move", action = "Move", composite = true },
        new Spec { row = "KeybindRow_Sprint", action = "Sprint" },
        new Spec { row = "KeybindRow_Back Turn", action = "AutoRotate" },
        new Spec { row = "KeybindRow_Interact", action = "Interact" },
        new Spec { row = "KeybindRow_Use Painkiller", action = "Heal" },
        new Spec { row = "KeybindRow_Use Medic Kit", action = "Heal" },
        new Spec { row = "KeybindRow_Inventory", action = "Inventory" },
        new Spec { row = "KeybindRow_Map", action = "Map" },
        new Spec { row = "KeybindRow_Aim (ADS)", action = "Aim", mirror = "HeavyAttack" },
        new Spec { row = "KeybindRow_Fire / Attack", action = "Fire", mirror = "LightAttack" },
        new Spec { row = "KeybindRow_Reload", action = "Reload" },
        new Spec { row = "KeybindRow_Melee Weapon", action = "Weapon3" },
        new Spec { row = "KeybindRow_Ranged Weapon", action = "Weapon1" },
    };

    static readonly string[] partNames = { "up", "down", "left", "right" };
    static readonly string[] partLabels = { "Forward", "Back", "Turn Left", "Turn Right" };

    class Row
    {
        public Spec spec;
        public Transform root;
        public Text label;
        public string labelText;
        public Image keyImage;
        public Sprite defaultSprite;
        public Vector2 defaultSize;
        public bool defaultPreserve;
        public Text keyText;
        public GameObject cluster;
    }

    readonly List<Row> rows = new List<Row>();
    InputActionRebindingExtensions.RebindingOperation operation;
    Row busyRow;
    GameObject confirmPanel;
    bool built;
    InputAction pendingPause;

    InputActionAsset Asset => ControlBindingStore.FindAsset(inputAsset);

    void OnEnable()
    {
        if (!built) Build();
        RefreshAll();
    }

    void OnDisable()
    {
        CancelRebind();
        // the panel closed before the delayed pause re-enable ran: do it now, or the pause key would stay dead
        if (pendingPause != null) { pendingPause.Enable(); pendingPause = null; }
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // ---------------- setup ----------------

    void Build()
    {
        built = true;
        foreach (var spec in specs)
        {
            var t = FindDeep(transform, spec.row);
            if (t == null) continue;
            var row = new Row { spec = spec, root = t, label = t.GetComponentInChildren<Text>(true) };
            row.labelText = row.label != null ? row.label.text : "";
            var key = t.Find("KeyLabel");
            if (key != null)
            {
                row.keyImage = key.GetComponent<Image>();
                row.defaultSprite = row.keyImage.sprite;
                row.defaultSize = ((RectTransform)key).sizeDelta;
                row.defaultPreserve = row.keyImage.preserveAspect;
                var go = new GameObject("KeyText", typeof(RectTransform));
                go.transform.SetParent(key, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(8, 4); rt.offsetMax = new Vector2(-8, -6);
                row.keyText = go.AddComponent<Text>();
                row.keyText.font = row.label != null ? row.label.font : null;
                row.keyText.fontSize = 24;
                row.keyText.alignment = TextAnchor.MiddleCenter;
                row.keyText.color = keyTextColor;
                row.keyText.raycastTarget = false;
                row.keyText.horizontalOverflow = HorizontalWrapMode.Overflow;
                go.SetActive(false);
            }
            var click = t.gameObject.GetComponent<ControlsRebindRow>();
            if (click == null) click = t.gameObject.AddComponent<ControlsRebindRow>();
            var r = row;
            click.onClick = () => StartRebind(r);
            rows.Add(row);
        }
        BuildDefaultsButton();
        BuildConfirmPanel();
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }

    // ---------------- bindings ----------------

    static int KeyboardBinding(InputAction action)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var b = action.bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;
            if (b.groups != null && b.groups.Contains("Keyboard&Mouse")) return i;
            string p = b.effectivePath ?? "";
            if (p.StartsWith("<Keyboard>") || p.StartsWith("<Mouse>")) return i;
        }
        return -1;
    }

    /// <summary>The first keyboard composite's part (Move: WASD, not the arrow keys or the gamepad stick).</summary>
    static int CompositePart(InputAction action, string part)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (!action.bindings[i].isComposite) continue;
            for (int j = i + 1; j < action.bindings.Count && action.bindings[j].isPartOfComposite; j++)
                if (action.bindings[j].name == part && (action.bindings[j].effectivePath ?? "").StartsWith("<Keyboard>")) return j;
        }
        return -1;
    }

    static List<(InputAction action, int index)> Targets(InputActionAsset asset, Spec spec)
    {
        var list = new List<(InputAction, int)>();
        var a = asset.FindAction(spec.action);
        if (a == null) return list;
        if (spec.composite)
        {
            foreach (var part in partNames) { int i = CompositePart(a, part); if (i >= 0) list.Add((a, i)); }
        }
        else
        {
            int i = KeyboardBinding(a);
            if (i >= 0) list.Add((a, i));
        }
        return list;
    }

    // ---------------- display ----------------

    void RefreshAll()
    {
        var asset = Asset;
        if (asset == null) return;
        foreach (var row in rows) Refresh(row, asset);
    }

    void Refresh(Row row, InputActionAsset asset)
    {
        if (row.label != null) row.label.text = row.labelText;
        if (row.keyImage == null) return;
        var targets = Targets(asset, row.spec);
        bool changed = false;
        foreach (var (action, index) in targets)
            if (!string.IsNullOrEmpty(action.bindings[index].overridePath)) changed = true;

        ClearCluster(row);
        if (!changed)
        {
            ShowSprite(row, row.defaultSprite, row.defaultSize);
            return;
        }

        if (row.spec.composite)
        {
            // forward on top; turn left, back, turn right below - like the WASD picture
            var parts = new List<string>();
            foreach (var (action, index) in targets) parts.Add(action.bindings[index].effectivePath);
            ShowCluster(row, parts);
        }
        else ShowKey(row, targets[0].action.bindings[targets[0].index].effectivePath);
    }

    /// <summary>The key's own keycap picture (the same style as the original icons) at the original icon height.</summary>
    void ShowKey(Row row, string path)
    {
        var sprite = KeySprite(path);
        if (sprite == null) { ShowKeyText(row, KeyName(path)); return; }
        float h = row.defaultSize.y;
        ShowSprite(row, sprite, new Vector2(h * sprite.rect.width / sprite.rect.height, h));
    }

    void ShowSprite(Row row, Sprite sprite, Vector2 size)
    {
        row.keyImage.enabled = true;
        row.keyImage.sprite = sprite;
        row.keyImage.type = Image.Type.Simple;
        row.keyImage.preserveAspect = true;
        ((RectTransform)row.keyImage.transform).sizeDelta = size;
        row.keyText.gameObject.SetActive(false);
    }

    void ShowKeyText(Row row, string text)
    {
        row.keyImage.enabled = true;
        row.keyImage.sprite = blankKey;
        row.keyImage.type = Image.Type.Sliced;
        row.keyImage.preserveAspect = false;
        row.keyText.text = text;
        row.keyText.gameObject.SetActive(true);
        float w = Mathf.Max(row.defaultSize.y * 1.05f, row.keyText.preferredWidth + 26f);
        ((RectTransform)row.keyImage.transform).sizeDelta = new Vector2(w, row.defaultSize.y);
    }

    void ShowCluster(Row row, List<string> parts)
    {
        // parts: up, down, left, right
        float h = row.defaultSize.y * 0.5f, gap = 2f;
        Sprite[] sprites = new Sprite[4];
        float[] widths = new float[4];
        for (int i = 0; i < 4; i++)
        {
            var ks = i < parts.Count ? ClusterSprite(parts[i]) : null;
            sprites[i] = ks != null ? ks : blankKey;
            widths[i] = sprites[i] != null ? h * sprites[i].rect.width / sprites[i].rect.height : h;
        }
        float bottom = widths[2] + widths[1] + widths[3] + gap * 2f;
        float total = Mathf.Max(bottom, widths[0]);
        var keyRt = (RectTransform)row.keyImage.transform;
        keyRt.sizeDelta = new Vector2(total, row.defaultSize.y);
        row.keyImage.enabled = false;           // its children (the cluster) still draw
        row.keyText.gameObject.SetActive(false);

        var cluster = new GameObject("KeyCluster", typeof(RectTransform));
        cluster.transform.SetParent(keyRt, false);
        var crt = (RectTransform)cluster.transform;
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
        row.cluster = cluster;

        float x = (total - bottom) * 0.5f;
        float backX = x + widths[2] + gap;
        PlaceKey(crt, sprites[2], widths[2], h, x, 0f);                                   // turn left
        PlaceKey(crt, sprites[1], widths[1], h, backX, 0f);                               // back
        PlaceKey(crt, sprites[3], widths[3], h, backX + widths[1] + gap, 0f);             // turn right
        PlaceKey(crt, sprites[0], widths[0], h, backX + (widths[1] - widths[0]) * 0.5f, h); // forward, above "back"
    }

    void PlaceKey(RectTransform parent, Sprite sprite, float w, float h, float x, float y)
    {
        var go = new GameObject("Key", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
    }

    void ClearCluster(Row row)
    {
        if (row.cluster != null) Destroy(row.cluster);
        row.cluster = null;
    }

    /// <summary>A square keycap with a bigger letter for the WASD-style cluster (like the original picture); else the normal one.</summary>
    Sprite ClusterSprite(string path)
    {
        EnsureLookup();
        if (keyLookup.TryGetValue("keyc_" + KeyFile(KeyName(path)).Substring(4), out var big)) return big;
        return KeySprite(path);
    }

    Sprite KeySprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (path.StartsWith("<Mouse>"))
        {
            if (path.EndsWith("leftButton")) return leftMouse;
            if (path.EndsWith("rightButton")) return rightMouse;
            if (path.EndsWith("middleButton")) return middleMouse;
        }
        EnsureLookup();
        keyLookup.TryGetValue(KeyFile(KeyName(path)), out var s);
        return s;
    }

    void EnsureLookup()
    {
        if (keyLookup != null) return;
        keyLookup = new Dictionary<string, Sprite>();
        if (keySprites != null) foreach (var k in keySprites) if (k != null) keyLookup[k.name] = k;
    }

    /// <summary>File name of a generated keycap: key_ + the text's UTF-16 codes (e.g. "K" -> key_004B).</summary>
    static string KeyFile(string text)
    {
        var parts = new List<string>();
        foreach (char c in text) parts.Add(((int)c).ToString("X4"));
        return "key_" + string.Join("_", parts);
    }

    public static string KeyName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "-";
        string n = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        if (path.StartsWith("<Mouse>"))
        {
            if (path.EndsWith("leftButton")) return "LMB";
            if (path.EndsWith("rightButton")) return "RMB";
            if (path.EndsWith("middleButton")) return "MMB";
            if (path.EndsWith("forwardButton")) return "M5";
            if (path.EndsWith("backButton")) return "M4";
            return n;
        }
        if (n.StartsWith("Numpad ")) n = "Num " + n.Substring(7);
        switch (n)
        {
            case "Left Shift": case "Shift": return "Shift";
            case "Right Shift": return "R Shift";
            case "Left Control": case "Left Ctrl": case "Control": case "Ctrl": return "Ctrl";
            case "Right Control": case "Right Ctrl": return "R Ctrl";
            case "Left Alt": case "Alt": return "Alt";
            case "Right Alt": case "Alt Gr": return "R Alt";
            case "Up Arrow": return "Up";
            case "Down Arrow": return "Down";
            case "Left Arrow": return "Left";
            case "Right Arrow": return "Right";
            case "Backspace": return "Back";
            case "Caps Lock": return "Caps";
            case "Page Up": return "PgUp";
            case "Page Down": return "PgDn";
            case "Escape": return "Esc";
            case "Return": return "Enter";
            case "Backquote": return "`";
            case "Minus": return "-";
            case "Equals": return "=";
            case "Left Bracket": return "[";
            case "Right Bracket": return "]";
            case "Semicolon": return ";";
            case "Quote": return "'";
            case "Comma": return ",";
            case "Period": return ".";
            case "Slash": return "/";
            case "Backslash": return "\\";
        }
        return n.Length == 1 ? n.ToUpperInvariant() : n;
    }

    // ---------------- rebinding ----------------

    void StartRebind(Row row)
    {
        if (operation != null || (confirmPanel != null && confirmPanel.activeSelf)) return;
        var asset = Asset;
        if (asset == null) return;
        var targets = Targets(asset, row.spec);
        if (targets.Count == 0) return;
        busyRow = row;
        RebindStep(row, targets, 0);
    }

    void RebindStep(Row row, List<(InputAction action, int index)> targets, int step)
    {
        var (action, index) = targets[step];
        string what = row.spec.composite ? partLabels[System.Array.IndexOf(partNames, action.bindings[index].name)] : row.labelText;
        if (row.label != null) row.label.text = "Press a key for " + what + "   (Esc to cancel)";
        ClearCluster(row);
        ShowKeyText(row, "...");

        // the action (and the pause key, whose Esc cancels here) must not fire while listening
        bool wasEnabled = action.enabled;
        action.Disable();
        var pause = Asset.FindAction("Pause");
        bool pauseWasEnabled = pause != null && pause.enabled;
        pause?.Disable();
        string oldPath = action.bindings[index].effectivePath;

        operation = action.PerformInteractiveRebinding(index)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithControlsExcluding("<Pointer>")
            .WithControlsExcluding("<Gamepad>")
            .WithControlsExcluding("<Joystick>")
            .WithControlsExcluding("<Keyboard>/anyKey")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.08f)
            .OnComplete(op =>
            {
                Finish(op, action, wasEnabled, pause, pauseWasEnabled);
                string newPath = action.bindings[index].effectivePath;
                if (newPath != oldPath) ResolveConflicts(action, index, oldPath, newPath);
                if (row.spec.mirror != null) Mirror(row.spec.mirror, newPath);
                ControlBindingStore.Save(Asset);
                if (step + 1 < targets.Count) RebindStep(row, targets, step + 1);
                else { busyRow = null; RefreshAll(); }
            })
            .OnCancel(op =>
            {
                Finish(op, action, wasEnabled, pause, pauseWasEnabled);
                busyRow = null;
                RefreshAll();
            });
        operation.Start();
    }

    void Finish(InputActionRebindingExtensions.RebindingOperation op, InputAction action, bool wasEnabled, InputAction pause, bool pauseWasEnabled)
    {
        op.Dispose();
        if (operation == op) operation = null;
        if (wasEnabled) action.Enable();
        // re-enable the pause key a frame later: the Esc that cancelled must not also open / close the pause menu
        if (pause != null && pauseWasEnabled)
        {
            if (isActiveAndEnabled) { pendingPause = pause; StartCoroutine(EnableNextFrame(pause)); }
            else pause.Enable();
        }
    }

    System.Collections.IEnumerator EnableNextFrame(InputAction a)
    {
        yield return null;
        yield return null;
        a.Enable();
        if (pendingPause == a) pendingPause = null;
    }

    void CancelRebind()
    {
        if (operation != null) { operation.Cancel(); operation = null; }
    }

    void Mirror(string actionName, string path)
    {
        var a = Asset.FindAction(actionName);
        if (a == null) return;
        int i = KeyboardBinding(a);
        if (i < 0) return;
        a.ApplyBindingOverride(i, new InputBinding { overridePath = path });
    }

    /// <summary>The new key was already another row's: that row gets the old key (a swap), so no key does two jobs.</summary>
    void ResolveConflicts(InputAction changed, int changedIndex, string oldPath, string newPath)
    {
        var asset = Asset;
        foreach (var spec in specs)
        {
            foreach (var (action, index) in Targets(asset, spec))
            {
                if (action == changed && index == changedIndex) continue;
                if (action.bindings[index].effectivePath != newPath) continue;
                action.ApplyBindingOverride(index, new InputBinding { overridePath = oldPath });
                if (spec.mirror != null) Mirror(spec.mirror, oldPath);
            }
        }
    }

    // ---------------- restore defaults ----------------

    void BuildDefaultsButton()
    {
        var topBar = FindDeep(transform, "TopBar");
        var back = topBar != null ? topBar.Find("BackToSettingsRow") : null;
        if (back == null) return;
        var go = Instantiate(back.gameObject, topBar);
        go.name = "RestoreDefaultsRow";
        var le = go.GetComponent<LayoutElement>();
        if (le != null) le.preferredWidth = 280f;
        var t = go.GetComponentInChildren<Text>(true);
        if (t != null) { t.text = "Restore Defaults"; t.alignment = TextAnchor.MiddleCenter; }
        var b = go.GetComponent<Button>();
        if (b != null)
        {
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(() => { CancelRebind(); RefreshAll(); if (confirmPanel != null) confirmPanel.SetActive(true); });
        }
    }

    void BuildConfirmPanel()
    {
        var back = FindDeep(transform, "BackToSettingsRow");
        Font font = back != null ? back.GetComponentInChildren<Text>(true).font : null;
        Color textColor = back != null ? back.GetComponentInChildren<Text>(true).color : Color.white;

        confirmPanel = new GameObject("RestoreDefaultsConfirm", typeof(RectTransform));
        confirmPanel.transform.SetParent(transform, false);
        var le = confirmPanel.AddComponent<LayoutElement>(); le.ignoreLayout = true;
        var rt = (RectTransform)confirmPanel.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var shade = confirmPanel.AddComponent<Image>(); shade.color = new Color(0f, 0f, 0f, 0.78f);   // also blocks clicks behind it

        var box = new GameObject("Box", typeof(RectTransform));
        box.transform.SetParent(confirmPanel.transform, false);
        var brt = (RectTransform)box.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.sizeDelta = new Vector2(900f, 260f);
        var bimg = box.AddComponent<Image>(); bimg.color = new Color(0.12f, 0.1f, 0.08f, 0.96f);
        var outline = box.AddComponent<Outline>(); outline.effectColor = new Color(textColor.r, textColor.g, textColor.b, 0.5f); outline.effectDistance = new Vector2(2f, -2f);

        var msg = new GameObject("Message", typeof(RectTransform));
        msg.transform.SetParent(box.transform, false);
        var mrt = (RectTransform)msg.transform;
        mrt.anchorMin = new Vector2(0f, 0.45f); mrt.anchorMax = Vector2.one; mrt.offsetMin = new Vector2(40f, 0f); mrt.offsetMax = new Vector2(-40f, -20f);
        var mt = msg.AddComponent<Text>();
        mt.font = font; mt.fontSize = 34; mt.color = textColor; mt.alignment = TextAnchor.MiddleCenter;
        mt.text = "Are you sure you want to restore the default controls?";

        MakeChoice(box.transform, back, "Yes", new Vector2(-150f, 60f), () =>
        {
            ControlBindingStore.ResetToDefaults(Asset);
            // the camera settings on this page too: aim sensitivity, free camera + its speed, ammo counter
            var options = GetComponentInParent<OptionsManager>(true);
            if (options == null) options = OptionsManager.instance;
            if (options != null) options.ResetControlSettings();
            confirmPanel.SetActive(false);
            RefreshAll();
        });
        MakeChoice(box.transform, back, "No", new Vector2(150f, 60f), () => confirmPanel.SetActive(false));
        confirmPanel.SetActive(false);
    }

    void MakeChoice(Transform parent, Transform template, string label, Vector2 pos, UnityEngine.Events.UnityAction act)
    {
        if (template == null) return;
        var go = Instantiate(template.gameObject, parent);
        go.name = label + "Button";
        var le = go.GetComponent<LayoutElement>(); if (le != null) le.ignoreLayout = true;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 64f); rt.anchoredPosition = pos;
        var t = go.GetComponentInChildren<Text>(true);
        if (t != null) { t.text = label; t.alignment = TextAnchor.MiddleCenter; var trt = (RectTransform)t.transform; trt.offsetMin = trt.offsetMax = Vector2.zero; }
        var b = go.GetComponent<Button>();
        if (b != null) { b.onClick = new Button.ButtonClickedEvent(); b.onClick.AddListener(act); }
    }
}
