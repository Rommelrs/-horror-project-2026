using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    static bool m_pause = false;
    public static bool IsPaused
    {
        get
        {
            return m_pause;
        }
        set
        {
            if (m_pause != value)
            {
                //Invoke the events
                if (value) OnGamePaused?.Invoke();
                else OnGameResumed?.Invoke();

                //Set the tiemscale to 0 if game is paused, set it to 1 if game is resumed
                if (value)
                    Time.timeScale = 0f;
                else
                    Time.timeScale = 1f;

                //Enable Cursor if game is paused, Disable it if game is resumed
                if (value)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            m_pause = value;
        }
    }

    public delegate void GamePaused();
    public static event GamePaused OnGamePaused;

    public delegate void GameResumed();
    public static event GameResumed OnGameResumed;

    [SerializeField] InputActionReference navigateInput;

    public bool gameStarted = false;
    public bool disableCursorOnStart;

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        navigateInput.action.Enable();

        //Unregister from the event
        MenuPanelSwitcher.OnMenuPanelSwitched += OnMenuPanelSwitched;
    }

    private void OnDestroy()
    {
        //Unregister from the event
        MenuPanelSwitcher.OnMenuPanelSwitched -= OnMenuPanelSwitched;
    }

    private void Start()
    {
        //Initialize
        IsPaused = false;
        Time.timeScale = 1f;

        //Check if the cursor should be disabled on start
        if (disableCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        //Check if Navigate input is pressed then update UI selection
        Vector2 navigate = navigateInput.action.ReadValue<Vector2>();
        if (navigate.magnitude > 0)
        {
            UpdateCurrentSelection();
        }
    }

    //Update UI Selection if UI is enabled
    void UpdateCurrentSelection()
    {
        if (EventSystem.current)
        {
            GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
            if (selectedObject == null || !selectedObject.activeInHierarchy)
            {
                //Select new object
                Selectable[] selectables = FindObjectsOfType<Selectable>();
                if (selectables != null && selectables.Length > 0)
                {
                    for (int i = 0; i < selectables.Length; i++)
                    {
                        if (selectables[i].gameObject.activeInHierarchy && selectables[i].interactable)
                        {
                            EventSystem.current.SetSelectedGameObject(selectables[i].gameObject);
                            break;
                        }
                    }
                }
            }
        }
    }

    //Check if Menu Panel is switched using a controller then update the currect selection
    public void OnMenuPanelSwitched()
    {
        if (ControllerIsConnected())
        {
            UpdateCurrentSelection();
        }
    }

    public bool ControllerIsConnected()
    {
        var gamepads = Gamepad.all;
        if (gamepads.Count > 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
