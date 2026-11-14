using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Click / hover on one row of the Options > Controls page (added at runtime by ControlsRebindUI).</summary>
public class ControlsRebindRow : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action onClick;
    Image background;
    Color normal;
    bool hover;

    void Awake()
    {
        background = GetComponent<Image>();
        if (background != null) normal = background.color;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (UISoundManager.instance != null) UISoundManager.instance.PlayButtonClickSound();
        onClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hover = true; Tint();
        if (UISoundManager.instance != null) UISoundManager.instance.PlayButtonHoverSound();
    }
    public void OnPointerExit(PointerEventData eventData) { hover = false; Tint(); }
    void OnDisable() { hover = false; Tint(); }

    void Tint()
    {
        if (background == null) return;
        var c = normal;
        if (hover) c.a = Mathf.Min(1f, normal.a + 0.12f);
        background.color = c;
    }
}
