using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuilds the Store scene's NavMesh (the "Environment" NavMeshSurface, which collects physics colliders) after the
/// store makeover moved the furniture: the old bench / shelves leave holes and ledges in the old mesh.
/// The new data replaces the old asset at the same path.
/// </summary>
public static class StoreNavMeshRebake
{
    [MenuItem("Tools/Store Makeover/Rebake Store NavMesh")]
    public static void Rebake()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Store.unity");
        if (!scene.isLoaded) { Debug.LogError("[StoreNavMesh] open the Store scene first"); return; }
        NavMeshSurface surface = null;
        foreach (var r in scene.GetRootGameObjects())
            foreach (var s in r.GetComponentsInChildren<NavMeshSurface>(false))
                if (s.isActiveAndEnabled) surface = s;
        if (surface == null) { Debug.LogError("[StoreNavMesh] no active NavMeshSurface"); return; }

        string path = surface.navMeshData != null ? AssetDatabase.GetAssetPath(surface.navMeshData) : "";
        surface.BuildNavMesh();
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            AssetDatabase.SaveAssets();
        }
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[StoreNavMesh] rebuilt on '" + surface.name + "' -> " + (string.IsNullOrEmpty(path) ? "(scene only)" : path));
    }
}
