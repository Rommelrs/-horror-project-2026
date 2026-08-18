using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class ChairInteractable : Interactable
{
    [Header("Chair Setup")]
    [Tooltip("Where the player will be positioned when sitting. Point it facing the direction you want the player to look.")]
    [SerializeField] Transform sitPoint;
    [Tooltip("Where the player is moved to when standing up (e.g. in front of the chair).")]
    [SerializeField] Transform standUpPoint;
    [Tooltip("First person Cinemachine Virtual Camera for the sitting view.")]
    [SerializeField] CinemachineVirtualCamera sitCam;
    [Tooltip("Can the player stand up by pressing E?")]
    [SerializeField] bool canStandUp = true;

    [Header("Input")]
    [SerializeField] InputActionReference exitSitInput;

    [Header("Events")]
    public UnityEvent OnSitDown;
    public UnityEvent OnStandUp;

    bool isSitting = false;
    Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        // Make sure sit cam starts inactive
        if (sitCam != null)
            sitCam.Priority = 0;
    }

    public override void Interacted()
    {
        base.Interacted();

        if (isSitting) return;

        StartCoroutine(Co_SitDown());
    }

    IEnumerator Co_SitDown()
    {
        isSitting = true;

        // Disable trigger so player can't interact again while sitting
        if (triggerCollider != null)
            triggerCollider.enabled = false;

        // Disable player movement and weapon
        Player.instance.pauseMovement = true;
        Player.instance.playerWeaponSystem.weaponIsEnabled = false;
        Player.instance.playerWeaponSystem.ExitOutOfAiming();

        // Disable character controller so we can teleport cleanly
        Player.instance.controller.enabled = false;

        yield return new WaitForEndOfFrame();

        // Snap player to sit point
        Player.instance.transform.position = sitPoint.position;
        Player.instance.transform.rotation = sitPoint.rotation;

        yield return new WaitForEndOfFrame();

        Player.instance.controller.enabled = true;

        // Activate sit camera
        if (sitCam != null)
            sitCam.Priority = 100;

        OnSitDown?.Invoke();

        // Listen for exit input
        if (canStandUp && exitSitInput != null)
            exitSitInput.action.performed += OnExitPressed;
    }

    void OnExitPressed(InputAction.CallbackContext ctx)
    {
        if (!isSitting) return;
        StartCoroutine(Co_StandUp());
    }

    IEnumerator Co_StandUp()
    {
        // Unsubscribe immediately to prevent double-trigger
        if (exitSitInput != null)
            exitSitInput.action.performed -= OnExitPressed;

        // Deactivate sit camera
        if (sitCam != null)
            sitCam.Priority = 0;

        yield return new WaitForEndOfFrame();

        // Teleport player to stand up point
        if (standUpPoint != null)
        {
            Player.instance.controller.enabled = false;
            yield return new WaitForEndOfFrame();
            Player.instance.transform.position = standUpPoint.position;
            Player.instance.transform.rotation = standUpPoint.rotation;
            yield return new WaitForEndOfFrame();
            Player.instance.controller.enabled = true;
        }

        // Re-enable player
        Player.instance.pauseMovement = false;
        Player.instance.playerWeaponSystem.weaponIsEnabled = true;

        isSitting = false;

        // Re-enable trigger collider
        if (triggerCollider != null)
            triggerCollider.enabled = true;

        OnStandUp?.Invoke();
    }

    // Call this from external scripts if you want to force stand up (e.g. cutscene end)
    public void ForceStandUp()
    {
        if (isSitting)
            StartCoroutine(Co_StandUp());
    }

    public bool IsSitting() => isSitting;
}
