using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class MenuPanelSwitcher : MonoBehaviour
{
    //Panels
    [SerializeField] GameObject[] panels;

    //Event
    public static Action OnMenuPanelSwitched;

    //Switch Panel by index
    public void SwitchPanel(int panelIndex)
    {
        for (int i = 0; i < panels.Length; i++)
            panels[i].SetActive(i == panelIndex);

        // Invoke the event
        OnMenuPanelSwitched?.Invoke();
    }

    //Switch Panel by name
    public void SwitchPanel(string panelName)
    {
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i].name == panelName)
            {
                SwitchPanel(panels[i]);
                break;
            }
        }
    }

    //Switch Panel by GameObject
    public void SwitchPanel(GameObject panel)
    {
        for (int i = 0; i < panels.Length; i++)
            panels[i].SetActive(panels[i] == panel);

        // Invoke the event
        OnMenuPanelSwitched?.Invoke();
    }

    //Disable all panels
    public void DisableAllPanels()
    {
        for (int i = 0; i < panels.Length; i++)
            panels[i].SetActive(false);
    }
}
