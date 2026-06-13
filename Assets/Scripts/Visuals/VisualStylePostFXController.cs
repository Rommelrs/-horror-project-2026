using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class VisualStylePostFXController : MonoBehaviour
{
    [Header("Scene References")]
    public Camera targetCamera;
    public PostProcessLayer postProcessLayer;
    public PostProcessVolume colorGradingVolume;
    public PostProcessProfile colorGradingProfile;

    [Header("Profile Asset")]
    public bool createProfileIfMissing = true;
    public string profileAssetPath = "Assets/Scenes/Game_Profiles/VisualStyle_PostFX.asset";

    [Header("Color Grading")]
    public bool enableColorGrading = true;
    [Range(-100f, 100f)] public float temperature = -8f;
    [Range(-100f, 100f)] public float tint = -4f;
    [Range(-5f, 5f)] public float postExposure = -0.15f;
    [Range(-100f, 100f)] public float contrast = 18f;
    [Range(-100f, 100f)] public float saturation = -28f;
    public Color colorFilter = new Color(0.82f, 0.86f, 0.78f, 1f);

    [Header("Fog / Draw Distance")]
    public bool enableFog = true;
    public FogMode fogMode = FogMode.Exponential;
    [UnityEngine.Min(0f)] public float fogDensity = 0.028f;
    [UnityEngine.Min(0f)] public float fogStartDistance = 8f;
    [UnityEngine.Min(1f)] public float fogEndDistance = 85f;
    public Color fogColor = new Color(0.48f, 0.50f, 0.46f, 1f);

    [Header("Camera Draw Distance")]
    public bool controlCameraFarClip = true;
    [UnityEngine.Min(2f)] public float cameraFarClip = 95f;

    [ContextMenu("Apply Visual Style Now")]
    public void ApplyVisualStyleNow()
    {
        FindMissingReferences();
        EnsurePostProcessSetup();
        ApplyColorGrading();
        ApplyFogAndDrawDistance();
    }

    private void Reset()
    {
        FindMissingReferences();
        ApplyVisualStyleNow();
    }

    private void OnEnable()
    {
        ApplyVisualStyleNow();
    }

    private void OnValidate()
    {
        fogEndDistance = Mathf.Max(fogStartDistance + 1f, fogEndDistance);
        cameraFarClip = Mathf.Max(2f, cameraFarClip);
        ApplyVisualStyleNow();
    }

    private void FindMissingReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null && postProcessLayer == null)
            postProcessLayer = targetCamera.GetComponent<PostProcessLayer>();

        if (colorGradingVolume == null)
            colorGradingVolume = GetComponent<PostProcessVolume>();
    }

    private void EnsurePostProcessSetup()
    {
        if (colorGradingVolume == null)
            colorGradingVolume = gameObject.GetComponent<PostProcessVolume>();

        if (colorGradingVolume == null)
            colorGradingVolume = gameObject.AddComponent<PostProcessVolume>();

#if UNITY_EDITOR
        if (colorGradingProfile == null && createProfileIfMissing)
            colorGradingProfile = LoadOrCreateProfileAsset();
#endif

        if (colorGradingProfile == null && colorGradingVolume.sharedProfile != null)
            colorGradingProfile = colorGradingVolume.sharedProfile;

        colorGradingVolume.isGlobal = true;
        colorGradingVolume.priority = 20f;
        colorGradingVolume.weight = 1f;
        colorGradingVolume.blendDistance = 0f;
        colorGradingVolume.enabled = true;

        if (colorGradingProfile != null)
            colorGradingVolume.sharedProfile = colorGradingProfile;

        if (postProcessLayer != null)
        {
            postProcessLayer.enabled = true;
            postProcessLayer.volumeLayer = ~0;
            if (targetCamera != null)
                postProcessLayer.volumeTrigger = targetCamera.transform;
            postProcessLayer.fog.enabled = enableFog;
        }
    }

    private void ApplyColorGrading()
    {
        if (colorGradingProfile == null)
            return;

        if (!colorGradingProfile.TryGetSettings(out ColorGrading colorGrading))
            colorGrading = colorGradingProfile.AddSettings<ColorGrading>();

        colorGrading.enabled.overrideState = true;
        colorGrading.enabled.value = enableColorGrading;
        colorGrading.temperature.overrideState = true;
        colorGrading.temperature.value = temperature;
        colorGrading.tint.overrideState = true;
        colorGrading.tint.value = tint;
        colorGrading.postExposure.overrideState = true;
        colorGrading.postExposure.value = postExposure;
        colorGrading.contrast.overrideState = true;
        colorGrading.contrast.value = contrast;
        colorGrading.saturation.overrideState = true;
        colorGrading.saturation.value = saturation;
        colorGrading.colorFilter.overrideState = true;
        colorGrading.colorFilter.value = colorFilter;

#if UNITY_EDITOR
        EditorUtility.SetDirty(colorGradingProfile);
#endif
    }

    private void ApplyFogAndDrawDistance()
    {
        RenderSettings.fog = enableFog;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogStartDistance = fogStartDistance;
        RenderSettings.fogEndDistance = fogEndDistance;

        if (controlCameraFarClip && targetCamera != null)
            targetCamera.farClipPlane = Mathf.Max(targetCamera.nearClipPlane + 1f, cameraFarClip);
    }

#if UNITY_EDITOR
    private PostProcessProfile LoadOrCreateProfileAsset()
    {
        if (string.IsNullOrWhiteSpace(profileAssetPath))
            profileAssetPath = "Assets/Scenes/Game_Profiles/VisualStyle_PostFX.asset";

        var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profileAssetPath);
        if (profile != null)
            return profile;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return null;

        var directory = Path.GetDirectoryName(profileAssetPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(profile, profileAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return profile;
    }
#endif
}
