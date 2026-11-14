using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the player's remapped controls. The overrides live in PlayerPrefs (as the Input System's own JSON) and are put back on
/// the game's input asset (HorrorInput) whenever a scene loads, so every scene and every menu uses the same keys. The Options >
/// Controls page (ControlsRebindUI) changes them.
/// </summary>
public static class ControlBindingStore
{
    const string PrefsKey = "ControlBindingOverrides";
    const string AssetName = "HorrorInput";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplySaved();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySaved();

    /// <summary>The game's input asset (loaded with any scene that uses it).</summary>
    public static InputActionAsset FindAsset(InputActionAsset hint = null)
    {
        if (hint != null) return hint;
        foreach (var a in Resources.FindObjectsOfTypeAll<InputActionAsset>())
            if (a != null && a.name == AssetName) return a;
        return null;
    }

    public static void ApplySaved(InputActionAsset asset = null)
    {
        asset = FindAsset(asset);
        if (asset == null) return;
        string json = PlayerPrefs.GetString(PrefsKey, "");
        asset.RemoveAllBindingOverrides();
        if (!string.IsNullOrEmpty(json))
        {
            try { asset.LoadBindingOverridesFromJson(json); }
            catch (System.Exception e) { Debug.LogWarning("[Controls] saved key bindings could not be read, using the defaults: " + e.Message); }
        }
    }

    public static void Save(InputActionAsset asset)
    {
        if (asset == null) return;
        PlayerPrefs.SetString(PrefsKey, asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public static void ResetToDefaults(InputActionAsset asset)
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
        if (asset != null) asset.RemoveAllBindingOverrides();
    }
}
