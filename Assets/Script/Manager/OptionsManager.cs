using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System.Linq;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class OptionsManager : MonoBehaviour
{
    public static OptionsManager instance;
    [SerializeField] MenuPanelSwitcher menuPanelSwitcher;

    [Header("Language")]
    [SerializeField] TMP_Dropdown languageDropdown;

    [Header("Audio")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] Slider sfxSlider;
    [SerializeField] Slider musicSlider;

    [Header("Graphics")]
    [SerializeField] TMP_Dropdown resolutionDropdown;

    private Resolution[] resolutions;
    private List<Resolution> filteredResolutions;
    private float currentRefreshRate;
    private int currentResolutionIndex = 0;
    bool fullScreen = true;

    public delegate void OnLanguageUpdated(string languageCode);
    public static OnLanguageUpdated onLanguageUpdated;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        //Load Language
        LoadLanguage();

        // Set the default resolution to the current screen resolution
        resolutions = Screen.resolutions;
        filteredResolutions = new List<Resolution>();

        // Filter out duplicate resolutions
        resolutionDropdown.ClearOptions();
        currentRefreshRate = Screen.currentResolution.refreshRate;

        for (int i = 0; i < resolutions.Length; i++)
        {
            filteredResolutions.Add(resolutions[i]);
        }

        //Sort the resolutions by width and height
        filteredResolutions = filteredResolutions.OrderByDescending(x => x.width).ToList();

        List<string> options = new List<string>();
        for (int i = 0; i < filteredResolutions.Count; i++)
        {
            string resolutionOption = filteredResolutions[i].width + "x" + filteredResolutions[i].height;
            options.Add(resolutionOption);
            if (filteredResolutions[i].width == Screen.width && filteredResolutions[i].height == Screen.height)
                currentResolutionIndex = i;
        }

        // Add the resolution options to the resolution dropdown
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        //Load SFX and Music values
        LoadSFXValue();
        LoadMusicValue();

        //Subscribe to the Slider value change event
        sfxSlider.onValueChanged.AddListener(delegate { OnSoundEffectValueChagned(); });
        musicSlider.onValueChanged.AddListener(delegate { OnMusicSliderValueChanged(); });
    }

    private void OnDestroy()
    {
        //Unsubscribe to the Slider value change event
        sfxSlider.onValueChanged.RemoveListener(delegate { OnSoundEffectValueChagned(); });
        musicSlider.onValueChanged.RemoveListener(delegate { OnMusicSliderValueChanged(); });
    }

    #region Audio
    void OnSoundEffectValueChagned()
    {
        // Set the SFX volume based on the slider value
        float volumeTwo = sfxSlider.value;
        mixer.SetFloat("SFXVolume", Mathf.Log10(volumeTwo) * 20);
        //Save to PlayerPrefs
        PlayerPrefs.SetFloat("SFXVolume", volumeTwo);
    }

    void OnMusicSliderValueChanged()
    {
        // Set the music volume based on the slider value
        float volumeOne = musicSlider.value;
        mixer.SetFloat("MusicVolume", Mathf.Log10(volumeOne) * 20);
        //Save to PlayerPrefs
        PlayerPrefs.SetFloat("MusicVolume", volumeOne);
    }

    private void LoadMusicValue()
    {
        // Load the music volume from PlayerPrefs
        musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.85f);
        float volumeOne = musicSlider.value;
        mixer.SetFloat("MusicVolume", Mathf.Log10(volumeOne) * 20);
    }

    private void LoadSFXValue()
    {
        // Load the SFX volume from PlayerPrefs
        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.85f);
        float volumeTwo = sfxSlider.value;
        mixer.SetFloat("SFXVolume", Mathf.Log10(volumeTwo) * 20);
    }
    #endregion

    #region Graphics
    //Set fullscreen mode
    void SetFullscreen(bool value)
    {
        fullScreen = value;
    }

    //On Apply button click set the resolution and fullscreen mode
    public void OnApplyButtonClick()
    {
        //Set Fullscreen
        if (Screen.fullScreen != fullScreen)
            Screen.fullScreen = fullScreen;

        //Set Resolution
        Resolution resolution = filteredResolutions[resolutionDropdown.value];
        if (resolution.width != Screen.width && resolution.height != Screen.height)
            Screen.SetResolution(resolution.width, resolution.height, fullScreen);

        //Switch back to the main menu
        menuPanelSwitcher.SwitchPanel(0);
    }
    #endregion

    #region Language
    enum LanguageCodes
    {
        en,
        ar
    }

    string selectedLanguageCode;

    public static string GetLanguageCode()
    {
        if(instance != null)
            return instance.selectedLanguageCode;

        return string.Empty;
    }

    void LoadLanguage()
    {
        int languageIndex = PlayerPrefs.GetInt("Language", 0);
        selectedLanguageCode = ((LanguageCodes)languageIndex).ToString();
        languageDropdown.value = languageIndex;
        SetLanguage(languageIndex);
    }

    public void SetLanguage(int languageIndex)
    {
        PlayerPrefs.SetInt("Language", languageIndex);
        string languageCode = ((LanguageCodes)languageIndex).ToString();
        selectedLanguageCode = languageCode;
        StartCoroutine(ChangeLanguage(languageCode));
    }

    IEnumerator ChangeLanguage(string languageCode)
    {
        yield return LocalizationSettings.InitializationOperation;

        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(languageCode);
        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
        }

        onLanguageUpdated?.Invoke(selectedLanguageCode);
    }
    #endregion
}
