using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LocalizeFontUpdater : MonoBehaviour
{
    [SerializeField] Font enFont;
    [SerializeField] Font arFont;

    Text text;

    private void Awake()
    {
        text = GetComponent<Text>();
        OptionsManager.onLanguageUpdated += OnLanguageUpdated;
    }

    private void OnDestroy()
    {
        OptionsManager.onLanguageUpdated -= OnLanguageUpdated;
    }

    private void OnEnable()
    {
        OnLanguageUpdated(OptionsManager.GetLanguageCode());
    }

    public void OnLanguageUpdated(string languageCode)
    {
        switch (languageCode)
        {
            case "en":
                text.font = enFont;
                break;
            case "ar":
                text.font = arFont;
                break;
        }
    }
}
