using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Makes the Contrast / Brightness sliders on Custom/CustomUnlit materials apply only to the
/// layers set in Resources/ContrastSettings (Default by default).
///
/// A shader can't tell which layer an object is on, but each layer is drawn by cameras, so this
/// is decided per camera: just before a camera renders, it sets the global _ContrastDisabled
/// flag. A camera that renders any of the "contrast layers" shows the boost; one that doesn't
/// (e.g. the Inspection camera, which only renders the Inspection layer) shows the item neutral.
/// Installs itself automatically - nothing needs adding to a scene.
/// </summary>
public static class ItemContrastFilter
{
    static readonly int ContrastDisabledId = Shader.PropertyToID("_ContrastDisabled");
    const string SettingsResourceName = "ContrastSettings";

    static int contrastLayers = 1 << 0; // Default
    static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RuntimeInit()
    {
        Hook();
    }

#if UNITY_EDITOR
    // Also active in edit mode, so the Scene/Game views and material previews match play mode
    [InitializeOnLoadMethod]
    static void EditorInit()
    {
        Hook();
    }
#endif

    static void Hook()
    {
        LoadSettings();

        if (hooked) return;
        hooked = true;
        Camera.onPreRender += OnCameraPreRender;
    }

    static void LoadSettings()
    {
        ContrastSettings settings = Resources.Load<ContrastSettings>(SettingsResourceName);
        contrastLayers = settings != null ? settings.contrastLayers.value : 1 << 0;
    }

    static void OnCameraPreRender(Camera cam)
    {
#if UNITY_EDITOR
        // Pick up changes to the settings asset immediately while editing
        LoadSettings();
#endif
        bool boostVisible = (cam.cullingMask & contrastLayers) != 0;
        Shader.SetGlobalFloat(ContrastDisabledId, boostVisible ? 0f : 1f);
    }
}
