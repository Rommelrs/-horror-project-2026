using UnityEngine;

/// <summary>
/// Quick test script to open/close Save Menu
/// Press F1 to open, ESC to close
/// </summary>
public class TestSaveMenu : MonoBehaviour
{
    [SerializeField] private SaveMenuUI saveMenuUI;
    
    void Update()
    {
        // Press F1 to open Save Menu
        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (saveMenuUI != null)
            {
                saveMenuUI.OpenSaveMenu();
                Debug.Log("Opening Save Menu!");
            }
            else
            {
                Debug.LogWarning("SaveMenuUI not assigned!");
            }
        }
        
        // Press ESC to close
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (saveMenuUI != null)
            {
                saveMenuUI.CloseSaveMenu();
            }
        }
    }
}
