using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple script to open the load menu - attach to Load Game button
/// </summary>
public class OpenLoadMenuButton : MonoBehaviour
{
    private void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            // Force enable button (temporary - will be controlled by checking for saves later)
            btn.interactable = true;
        }
    }
    
    public void OpenLoadMenu()
    {
        if (SaveLoadMenuManager.instance != null)
        {
            SaveLoadMenuManager.instance.OpenLoadMenu();
        }
        else
        {
            Debug.LogError("SaveLoadMenuManager not found! Make sure it exists in the scene.");
        }
    }
}
