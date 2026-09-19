using UnityEngine;
using UnityEngine.UI;

// Swaps the Options panel between the settings list (General/Graphics rows) and
// the Controls (keybinds) view, in place, instead of opening a separate window.
public class OptionsCategorySwitcher : MonoBehaviour
{
    [SerializeField] private GameObject settingsGroup;
    [SerializeField] private GameObject controlsGroup;
    [SerializeField] private Button openControlsButton;
    [SerializeField] private Button backToSettingsButton;

    private void Awake()
    {
        if (openControlsButton != null)
            openControlsButton.onClick.AddListener(ShowControls);

        if (backToSettingsButton != null)
            backToSettingsButton.onClick.AddListener(ShowSettings);
    }

    public void ShowControls()
    {
        settingsGroup.SetActive(false);
        controlsGroup.SetActive(true);
    }

    public void ShowSettings()
    {
        controlsGroup.SetActive(false);
        settingsGroup.SetActive(true);
    }
}
