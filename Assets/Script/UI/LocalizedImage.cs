using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a different picture per game language (Options > Language): e.g. the map with English or Arabic street names.
/// Both pictures must be the same size and layout, so everything placed on top of them (map markers) stays in place.
/// </summary>
[RequireComponent(typeof(Image))]
public class LocalizedImage : MonoBehaviour
{
    [SerializeField] Sprite english;
    [SerializeField] Sprite arabic;

    Image image;

    void Awake() => image = GetComponent<Image>();

    void OnEnable()
    {
        OptionsManager.onLanguageUpdated += Apply;
        string code = OptionsManager.GetLanguageCode();
        Apply(string.IsNullOrEmpty(code) ? (PlayerPrefs.GetInt("Language", 0) == 1 ? "ar" : "en") : code);
    }

    void OnDisable() => OptionsManager.onLanguageUpdated -= Apply;

    void Apply(string languageCode)
    {
        if (image == null) image = GetComponent<Image>();
        Sprite s = languageCode == "ar" ? arabic : english;
        if (s != null && image.sprite != s) image.sprite = s;
    }
}
