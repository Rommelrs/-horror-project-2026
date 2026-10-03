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

    [Header("Controls")]
    [Tooltip("Aim sensitivity (mouse/stick look speed while aiming).")]
    [SerializeField] Slider sensitivitySlider;
    [Tooltip("Switches between the classic tank camera and the free (mouse-controlled) camera.")]
    [SerializeField] Toggle freeCameraToggle;
    [Tooltip("Look speed of the free camera.")]
    [SerializeField] Slider cameraSpeedSlider;

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
        if (sensitivitySlider != null)
        {
            LoadSensitivityValue();
            sensitivitySlider.onValueChanged.AddListener(delegate { OnSensitivityChanged(); });
        }

        if (freeCameraToggle != null)
        {
            freeCameraToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(CameraSystem.FreeCameraPrefKey, 0) == 1);
            freeCameraToggle.onValueChanged.AddListener(OnFreeCameraToggled);
        }

        if (cameraSpeedSlider != null)
        {
            cameraSpeedSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(CameraSystem.FreeCameraSpeedPrefKey, 1f));
            cameraSpeedSlider.onValueChanged.AddListener(OnCameraSpeedChanged);
        }

        // Keep the toggle in step when the camera is switched with the hotkey instead
        if (CameraSystem.Instance != null)
            CameraSystem.Instance.FreeCameraChanged += OnFreeCameraChangedElsewhere;
    }

    private void OnDestroy()
    {
        if (CameraSystem.Instance != null)
            CameraSystem.Instance.FreeCameraChanged -= OnFreeCameraChangedElsewhere;

        //Unsubscribe to the Slider value change event
        sfxSlider.onValueChanged.RemoveListener(delegate { OnSoundEffectValueChagned(); });
        musicSlider.onValueChanged.RemoveListener(delegate { OnMusicSliderValueChanged(); });
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.RemoveListener(delegate { OnSensitivityChanged(); });
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

    void OnSensitivityChanged()
    {
        float value = sensitivitySlider.value;
        if (Player.instance != null)
            Player.instance.playerWeaponSystem.Sensitivity = value;
        PlayerPrefs.SetFloat("MouseSensitivity", value);
    }

    void LoadSensitivityValue()
    {
        float saved = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
        sensitivitySlider.value = saved;
        if (Player.instance != null)
            Player.instance.playerWeaponSystem.Sensitivity = saved;
    }

    #region Free Camera
    void OnFreeCameraToggled(bool enabled)
    {
        if (CameraSystem.Instance != null)
            CameraSystem.Instance.SetFreeCamera(enabled);
        else
            PlayerPrefs.SetInt(CameraSystem.FreeCameraPrefKey, enabled ? 1 : 0);
    }

    void OnFreeCameraChangedElsewhere(bool enabled)
    {
        if (freeCameraToggle != null)
            freeCameraToggle.SetIsOnWithoutNotify(enabled);
    }

    void OnCameraSpeedChanged(float value)
    {
        if (CameraSystem.Instance != null)
            CameraSystem.Instance.SetFreeCameraSpeed(value);
        else
            PlayerPrefs.SetFloat(CameraSystem.FreeCameraSpeedPrefKey, value);
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
