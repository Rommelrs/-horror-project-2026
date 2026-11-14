using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The cash register's own keys, pressed on the picture of the register (no separate button panel).
/// One key at a time is "pointed at" and only that one shows its outline: the key under the mouse, or the one reached
/// with the move keys (WASD / arrows / d-pad / left stick). Left click, Space, Enter, E or the gamepad's bottom button
/// presses it. The number keys of the keyboard still type straight away (Backspace = C) and move the outline to the key
/// they hit.
/// Lives on the "Keys" object under CashRegisterMenu (built by Tools > Store Makeover > Build Register Keypad).
/// </summary>
public class CashRegisterKeypad : MonoBehaviour
{
    [Tooltip("The open-drawer picture: while it shows, the outlines and the display text are hidden.")]
    public GameObject drawerPanel;
    [Tooltip("Outlines + display text (hidden while the drawer picture shows).")]
    public GameObject content;
    [Tooltip("The key that is pointed at when the register is opened.")]
    public int startDigit = 5;

    readonly List<CashRegisterKey> keys = new List<CashRegisterKey>();
    CashRegisterKey selected;
    /// <summary>The key that is pointed at right now.</summary>
    public CashRegisterKey Selected => selected;
    Vector2Int lastStick;
    float stickRepeatAt;

    void Awake()
    {
        GetComponentsInChildren(true, keys);
        foreach (var k in keys) k.pad = this;
    }

    void OnEnable()
    {
        Select(Find(CashRegisterKey.Kind.Digit, startDigit));
        lastStick = Vector2Int.zero;
    }

    public void Select(CashRegisterKey key)
    {
        if (key == null) return;
        selected = key;
        foreach (var k in keys) if (k != null) k.SetSelected(k == key);
    }

    CashRegisterKey Find(CashRegisterKey.Kind kind, int digit)
    {
        foreach (var k in keys)
            if (k != null && k.kind == kind && (kind != CashRegisterKey.Kind.Digit || k.digit == digit)) return k;
        return null;
    }

    void Update()
    {
        if (content != null && drawerPanel != null && content.activeSelf == drawerPanel.activeSelf) content.SetActive(!drawerPanel.activeSelf);
        var register = CashRegisterInteractable.instance;
        if (register == null || !register.CashRegisterMenuIsActive()) return;
        if (content != null && !content.activeSelf) return;

        var kb = Keyboard.current;
        var pad = Gamepad.current;

        // ---- moving the outline
        Vector2Int dir = Vector2Int.zero;
        if (kb != null)
        {
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) dir = Vector2Int.up;
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) dir = Vector2Int.down;
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) dir = Vector2Int.left;
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) dir = Vector2Int.right;
        }
        if (pad != null && dir == Vector2Int.zero)
        {
            Vector2 s = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
            Vector2Int now = Vector2Int.zero;
            if (s.magnitude > 0.55f) now = Mathf.Abs(s.x) > Mathf.Abs(s.y) ? (s.x > 0 ? Vector2Int.right : Vector2Int.left) : (s.y > 0 ? Vector2Int.up : Vector2Int.down);
            if (now != lastStick) { dir = now; stickRepeatAt = Time.unscaledTime + 0.4f; }
            else if (now != Vector2Int.zero && Time.unscaledTime >= stickRepeatAt) { dir = now; stickRepeatAt = Time.unscaledTime + 0.16f; }
            lastStick = now;
        }
        if (dir != Vector2Int.zero) Move(dir);

        // ---- pressing the key that is pointed at
        bool press = false;
        if (kb != null)
        {
            press = kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            // E is also "next line" of a subtitle: not while one is up
            if (kb.eKey.wasPressedThisFrame && (SubtitleManager.instance == null || !SubtitleManager.instance.IsSubtitleBusy())) press = true;
        }
        if (pad != null && pad.buttonSouth.wasPressedThisFrame) press = true;
        if (press && selected != null) selected.Press();

        // ---- typing on the keyboard's own number keys
        if (kb == null) return;
        for (int d = 0; d <= 9; d++)
            if (Digit(kb, d)) Type(Find(CashRegisterKey.Kind.Digit, d));
        if (kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame) Type(Find(CashRegisterKey.Kind.Clear, 0));
    }

    void Type(CashRegisterKey key)
    {
        if (key == null) return;
        Select(key);
        key.Press();
    }

    /// <summary>The nearest key in that direction (keys a long way off to the side count for less).</summary>
    public void Move(Vector2Int dir)
    {
        if (selected == null) { Select(Find(CashRegisterKey.Kind.Digit, startDigit)); return; }
        Vector2 from = selected.Rect.anchoredPosition, d = dir;
        CashRegisterKey best = null;
        // first a key that lies straight that way; only if there is none, one that is off to the side (C and CASH sit apart)
        for (int pass = 0; pass < 2 && best == null; pass++)
        {
            float bestScore = float.MaxValue;
            foreach (var k in keys)
            {
                if (k == null || k == selected) continue;
                Vector2 to = k.Rect.anchoredPosition - from;
                float along = Vector2.Dot(to, d);
                if (along < 8f) continue;
                float across = Mathf.Abs(to.x * d.y - to.y * d.x);
                if (across > (pass == 0 ? along * 0.6f + 10f : along * 2.2f + 30f)) continue;
                float score = along + across * 2.5f;
                if (score < bestScore) { bestScore = score; best = k; }
            }
        }
        if (best != null) Select(best);
    }

    static bool Digit(Keyboard kb, int d)
    {
        switch (d)
        {
            case 0: return kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame;
            case 1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case 2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case 3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case 4: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
            case 5: return kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
            case 6: return kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame;
            case 7: return kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame;
            case 8: return kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame;
            default: return kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame;
        }
    }
}
