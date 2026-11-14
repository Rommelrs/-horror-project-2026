using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The register's inspection screen for the new register ("Kasio" pictures): the code is typed on the keys of the
/// picture itself. Sets the closed / open pictures, hides the old dark key panel and code text, and lays an outline on
/// every keycap that does something (0-9, CASH = enter, C = clear) plus the typed code on the green display. Only the
/// key that is pointed at (mouse / move keys) shows its outline.
/// Store scene only (the Game scene has no register). Safe to run again.
/// </summary>
public static class RegisterKeypadBuilder
{
    const string Dir = "Assets/Sprites/Cash Register/";
    const float PicW = 1672f, PicH = 941f;      // the pictures; the screen layout is 1920 x 1080

    // keycaps in picture pixels: left, top, right, bottom
    static readonly (string name, CashRegisterKey.Kind kind, int digit, float l, float t, float r, float b)[] Keys =
    {
        ("Key 7", CashRegisterKey.Kind.Digit, 7, 800.7f, 272.3f, 848.3f, 319.0f),
        ("Key 8", CashRegisterKey.Kind.Digit, 8, 856.0f, 272.3f, 904.0f, 319.0f),
        ("Key 9", CashRegisterKey.Kind.Digit, 9, 912.0f, 272.3f, 960.0f, 319.0f),
        ("Key 4", CashRegisterKey.Kind.Digit, 4, 800.7f, 328.3f, 849.0f, 376.0f),
        ("Key 5", CashRegisterKey.Kind.Digit, 5, 856.7f, 328.3f, 905.0f, 376.0f),
        ("Key 6", CashRegisterKey.Kind.Digit, 6, 912.7f, 328.3f, 961.7f, 376.0f),
        ("Key 1", CashRegisterKey.Kind.Digit, 1, 800.7f, 385.7f, 849.0f, 434.3f),
        ("Key 2", CashRegisterKey.Kind.Digit, 2, 857.3f, 385.7f, 906.0f, 434.3f),
        ("Key 3", CashRegisterKey.Kind.Digit, 3, 913.7f, 385.7f, 963.3f, 434.3f),
        ("Key 0", CashRegisterKey.Kind.Digit, 0, 800.7f, 444.0f, 849.0f, 493.3f),
        ("Key CASH (enter)", CashRegisterKey.Kind.Enter, 0, 1033.3f, 444.0f, 1094.0f, 493.3f),
        ("Key C (clear)", CashRegisterKey.Kind.Clear, 0, 1023.3f, 211.0f, 1074.0f, 258.3f),
    };
    // the green display glass
    const float DispL = 730f, DispT = 52f, DispR = 1060f, DispB = 132f;

    static Sprite LoadSprite(string file, bool sliced)
    {
        string path = Dir + file;
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) { Debug.LogError("[RegisterKeypad] missing " + path); return null; }
        bool dirty = false;
        if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
        if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
        if (imp.wrapMode != TextureWrapMode.Clamp) { imp.wrapMode = TextureWrapMode.Clamp; dirty = true; }
        if (!imp.alphaIsTransparency && sliced) { imp.alphaIsTransparency = true; dirty = true; }
        var want = sliced ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        if (imp.textureCompression != want) { imp.textureCompression = want; dirty = true; }
        Vector4 border = sliced ? new Vector4(24, 24, 24, 24) : Vector4.zero;
        if (imp.spriteBorder != border) { imp.spriteBorder = border; dirty = true; }
        if (dirty) imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static RectTransform Place(GameObject go, Transform parent, float l, float t, float r, float b, float grow)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        float kx = 1920f / PicW, ky = 1080f / PicH;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2((l + r) * 0.5f * kx - 960f, 540f - (t + b) * 0.5f * ky);
        rt.sizeDelta = new Vector2((r - l) * kx + grow, (b - t) * ky + grow);
        return rt;
    }

    [MenuItem("Tools/Store Makeover/Build Register Keypad")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[RegisterKeypad] stop Play first"); return; }
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Store.unity");
        if (!scene.isLoaded) { Debug.LogError("[RegisterKeypad] open the Store scene"); return; }
        Transform menu = null; CashRegisterInteractable register = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "CashRegisterMenu") menu = t;
            foreach (var c in root.GetComponentsInChildren<CashRegisterInteractable>(true)) register = c;
        }
        if (menu == null || register == null) { Debug.LogError("[RegisterKeypad] CashRegisterMenu / CashRegisterInteractable not found"); return; }

        var closed = LoadSprite("Register Kasio closed.png", false);
        var open = LoadSprite("Register Kasio open.png", false);
        var outlineSprite = LoadSprite("key_outline.png", true);
        var fillSprite = LoadSprite("key_fill.png", true);
        if (closed == null || open == null || outlineSprite == null || fillSprite == null) return;

        var background = menu.Find("Background"); var drawer = menu.Find("CashRegisterDrawer");
        background.GetComponent<Image>().sprite = closed; EditorUtility.SetDirty(background.GetComponent<Image>());
        drawer.GetComponent<Image>().sprite = open; EditorUtility.SetDirty(drawer.GetComponent<Image>());
        foreach (Transform c in drawer) c.gameObject.SetActive(false);      // the old pasted-on map / key: the key is in the picture now

        // the old way of typing: the dark panel of buttons and its code text
        foreach (Transform c in menu)
        {
            var img = c.GetComponent<Image>();
            if (c.name == "Image" && img != null && img.sprite != null && img.sprite.name == "InputFieldBackground") c.gameObject.SetActive(false);
            if (c.name == "CodeEnter") c.gameObject.SetActive(false);
        }

        var oldKeys = menu.Find("Keys");
        if (oldKeys != null) Object.DestroyImmediate(oldKeys.gameObject);
        var keysGo = new GameObject("Keys", typeof(RectTransform));
        keysGo.layer = menu.gameObject.layer;
        var keysRt = keysGo.GetComponent<RectTransform>();
        keysRt.SetParent(menu, false);
        keysRt.anchorMin = Vector2.zero; keysRt.anchorMax = Vector2.one; keysRt.offsetMin = keysRt.offsetMax = Vector2.zero;
        keysRt.SetSiblingIndex(background.GetSiblingIndex() + 1);
        var pad = keysGo.AddComponent<CashRegisterKeypad>();
        pad.drawerPanel = drawer.gameObject;

        var content = new GameObject("Content", typeof(RectTransform));
        content.layer = keysGo.layer;
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.SetParent(keysRt, false);
        contentRt.anchorMin = Vector2.zero; contentRt.anchorMax = Vector2.one; contentRt.offsetMin = contentRt.offsetMax = Vector2.zero;
        pad.content = content;

        foreach (var k in Keys)
        {
            var go = new GameObject(k.name, typeof(RectTransform)); go.layer = keysGo.layer;
            Place(go, contentRt, k.l, k.t, k.r, k.b, 6f);
            var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.layer = keysGo.layer;
            var frt = fillGo.GetComponent<RectTransform>(); frt.SetParent(go.transform, false);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            var fill = fillGo.AddComponent<Image>(); fill.sprite = fillSprite; fill.type = Image.Type.Sliced; fill.raycastTarget = false; fill.color = new Color(1f, 0.92f, 0.6f, 0f);
            fill.pixelsPerUnitMultiplier = 1.6f;
            var outline = go.AddComponent<Image>(); outline.sprite = outlineSprite; outline.type = Image.Type.Sliced; outline.raycastTarget = true;
            outline.pixelsPerUnitMultiplier = 1.1f;
            var key = go.AddComponent<CashRegisterKey>();
            key.kind = k.kind; key.digit = k.digit; key.outline = outline; key.fill = fill;
            outline.color = new Color(key.selectedColor.r, key.selectedColor.g, key.selectedColor.b, 0f);      // shows only while it is the pointed-at key
        }

        // the typed code, on the display glass
        TMP_Text oldText = null;
        foreach (var tx in menu.GetComponentsInChildren<TMP_Text>(true)) if (tx.name == "CodeEnter") oldText = tx;
        var lcdGo = new GameObject("Display Text", typeof(RectTransform)); lcdGo.layer = keysGo.layer;
        Place(lcdGo, contentRt, DispL, DispT, DispR, DispB, -24f);
        var lcd = lcdGo.AddComponent<TextMeshProUGUI>();
        if (oldText != null) { lcd.font = oldText.font; lcd.fontSharedMaterial = oldText.fontSharedMaterial; }
        lcd.text = ""; lcd.fontSize = 84f; lcd.enableAutoSizing = false; lcd.alignment = TextAlignmentOptions.Right;
        lcd.characterSpacing = 18f; lcd.color = new Color(0.62f, 1f, 0.5f, 0.95f); lcd.raycastTarget = false; lcd.enableWordWrapping = false;
        lcd.overflowMode = TextOverflowModes.Overflow;

        var so = new SerializedObject(register);
        so.FindProperty("codeEnterTxt").objectReferenceValue = lcd;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(register);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[RegisterKeypad] built: " + Keys.Length + " keys, sibling index " + keysRt.GetSiblingIndex() + " (background " + background.GetSiblingIndex() + ", drawer " + drawer.GetSiblingIndex() + ")");
    }
}
