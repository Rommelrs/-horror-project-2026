using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnDestroy()
    {
        //Unsubscribe from the button click event
        button.onClick.RemoveListener(OnButtonClick);
    }

    private void Start()
    {
        //Subscribe to the button click event
        button.onClick.AddListener(OnButtonClick);
    }

    //On Button Click play button click sound
    private void OnButtonClick()
    {
        if (UISoundManager.instance)
            UISoundManager.instance.PlayButtonClickSound();
    }

    //On Pointer Enter play button hover sound
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UISoundManager.instance)
            UISoundManager.instance.PlayButtonHoverSound();
    }

    //On Pointer Exit play button hover exit sound
    public void OnPointerExit(PointerEventData eventData)
    {
        if (UISoundManager.instance)
            UISoundManager.instance.PlayButtonHoverExitSound();
    }
}
