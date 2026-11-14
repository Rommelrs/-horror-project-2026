using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One keycap of the register picture. Nothing is drawn on it until it is the one being pointed at (mouse over
/// it, or reached with the move keys): then its outline shows. A flash when it is pressed.</summary>
public class CashRegisterKey : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerDownHandler
{
    public enum Kind { Digit, Enter, Clear }
    public Kind kind;
    public int digit;
    public Image outline, fill;
    [Tooltip("Outline of the key that is pointed at.")]
    public Color selectedColor = new Color(1f, 0.86f, 0.4f, 1f);
    public Color pressColor = new Color(1f, 0.9f, 0.5f, 0.55f);
    [Tooltip("How much of the press colour lies on the keycap while it is the pointed-at key.")]
    [Range(0f, 1f)] public float selectedFill = 0.22f;

    [HideInInspector] public CashRegisterKeypad pad;
    bool selected;
    float shown, flash;

    public RectTransform Rect => (RectTransform)transform;

    void OnEnable() { flash = 0f; shown = selected ? 1f : 0f; Paint(); }

    public void SetSelected(bool on) { selected = on; }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (flash > 0f) flash = Mathf.Max(0f, flash - dt / 0.22f);
        shown = Mathf.MoveTowards(shown, selected ? 1f : 0f, dt / 0.07f);
        Paint();
    }

    void Paint()
    {
        if (outline != null) { var c = selectedColor; c.a *= shown; outline.color = c; }
        if (fill != null) { var c = pressColor; c.a *= Mathf.Max(flash, shown * selectedFill); fill.color = c; }
    }

    public void OnPointerEnter(PointerEventData e) { if (pad != null) pad.Select(this); }
    public void OnPointerMove(PointerEventData e) { if (pad != null && e.delta.sqrMagnitude > 0.01f) pad.Select(this); }
    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (pad != null) pad.Select(this);
        Press();
    }

    /// <summary>The key goes down: flash + tell the register.</summary>
    public void Press()
    {
        flash = 1f;
        var register = CashRegisterInteractable.instance;
        if (register == null) return;
        switch (kind)
        {
            case Kind.Digit: register.OnNumberButtonPressed(digit); break;
            case Kind.Enter: register.OnEnterButtonPressed(); break;
            case Kind.Clear: register.OnClearButtonPressed(); break;
        }
    }
}
