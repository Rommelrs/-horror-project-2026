using UnityEngine;

/// <summary>
/// Remembers which original objects were hidden when this group replaced them (e.g. the Game scene's store interior,
/// swapped for a copy of the Store scene's). Right-click the component header: "Revert" shows the originals again and
/// hides this group, "Reapply" does the opposite. Does nothing at runtime.
/// </summary>
public class SceneSwapRecord : MonoBehaviour
{
    [TextArea] public string note;
    public GameObject[] hiddenOriginals = new GameObject[0];

#if UNITY_EDITOR
    [ContextMenu("Revert (show originals, hide this)")]
    void Revert() => Set(false);

    [ContextMenu("Reapply (hide originals, show this)")]
    void Reapply() => Set(true);

    void Set(bool applied)
    {
        foreach (var g in hiddenOriginals)
            if (g != null) { UnityEditor.Undo.RecordObject(g, "Scene swap"); g.SetActive(!applied); }
        UnityEditor.Undo.RecordObject(gameObject, "Scene swap");
        foreach (Transform c in transform) c.gameObject.SetActive(applied);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}
