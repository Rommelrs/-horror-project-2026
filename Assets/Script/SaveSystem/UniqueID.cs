using UnityEngine;

/// <summary>
/// Gives an object a unique identifier for the save system
/// Add this component to any object that needs to be tracked in saves
/// </summary>
public class UniqueID : MonoBehaviour
{
    [SerializeField] private string id = "";
    
    public string ID
    {
        get
        {
            // Generate ID if empty
            if (string.IsNullOrEmpty(id))
            {
                id = System.Guid.NewGuid().ToString();
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            return id;
        }
    }
    
    // Generate a new unique ID (useful for duplicating objects)
    [ContextMenu("Generate New ID")]
    public void GenerateNewID()
    {
        id = System.Guid.NewGuid().ToString();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"Generated new ID for {gameObject.name}: {id}");
    }
}
