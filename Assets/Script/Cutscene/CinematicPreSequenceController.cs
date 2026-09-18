using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

/// <summary>
/// Place on a GameObject with a trigger collider.
/// When the player enters:
///   1. Player control is locked.
///   2. A Timeline plays (your silhouette camera cut + audio).
///   3. A subtitle fires after a configurable delay ("Is that you, Adel??").
///   4. The player auto-walks forward after a configurable delay.
///   5. When the Timeline ends, control hands off to CinematicKnockdownSequence.
///
/// SETUP CHECKLIST:
///   - Assign the PlayableDirector (your silhouette Timeline).
///   - Assign the KnockdownSequence to hand off to.
///   - Assign the SubtitleTrigger for the player voice line (optional).
///   - Set Subtitle Delay and Auto Walk Start Delay to match your Timeline length.
///   - Optionally assign Face Player Toward a Transform so the player faces the right
///     direction when they start running (e.g. point toward the runner's spawn).
///   - Make sure this GameObject has a Trigger Collider tagged correctly.
/// </summary>
public class CinematicPreSequenceController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PlayableDirector that plays your silhouette cinematic Timeline.")]
    [SerializeField] PlayableDirector director;
    [Tooltip("The CinematicKnockdownSequence to hand off to when this pre-sequence ends.")]
    [SerializeField] CinematicKnockdownSequence knockdownSequence;
    [Tooltip("Optional subtitle to fire mid-sequence (e.g. player saying 'Is that you, Adel??').")]
    [SerializeField] SubtitleTrigger preSequenceSubtitle;
    [Tooltip("Optional: if set, player is rotated to face this Transform when auto-walk begins.")]
    [SerializeField] Transform facePlayerToward;
    [Tooltip("Assign the PositionRoot transform under CameraSystem. Its Y rotation will be set to 180 when auto-walk starts.")]
    [SerializeField] Transform positionRoot;
    [Tooltip("Assign the CameraRotationOverride component (on any scene GameObject) to lock PositionRoot during auto-walk.")]
    [SerializeField] CameraRotationOverride cameraRotationOverride;

    [Header("Timing")]
    [Tooltip("Seconds after sequence starts before the subtitle fires.")]
    [SerializeField] float subtitleDelay = 1.5f;
    [Tooltip("Seconds after sequence starts before the player begins auto-walking.")]
    [SerializeField] float autoWalkStartDelay = 3.5f;
    [Tooltip("Player auto-walk speed (should match CinematicKnockdownSequence autoWalkSpeed).")]
    [SerializeField] float autoWalkSpeed = 2f;
    [Tooltip("Camera pitch angle when auto-walk starts (vertical tilt). 0 = level, negative = tilt up, positive = tilt down.")]
    [SerializeField] float autoWalkCameraPitch = 0f;
    [Tooltip("Camera horizontal offset relative to player during auto-walk. 0 = behind player, 90 = right side, -90 = left side.")]
    [SerializeField] float autoWalkCameraYawOffset = 0f;
    [Header("Trigger")]
    [SerializeField] bool triggerOnce = true;

    [Header("Events")]
    public UnityEvent OnPreSequenceStarted;
    public UnityEvent OnHandedOffToKnockdown;

    bool hasTriggered = false;
    bool autoWalking = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggerOnce && hasTriggered) return;
        hasTriggered = true;

        StartCoroutine(Co_RunPreSequence());
    }

    IEnumerator Co_RunPreSequence()
    {
        OnPreSequenceStarted?.Invoke();

        // ── Lock all player control ────────────────────────────────────────────
        Player.instance.playerWeaponSystem.weaponIsEnabled = false;
        Player.instance.playerWeaponSystem.ExitOutOfAiming();
        Player.instance.playerMovement.enabled = false;
        Player.instance.pauseMovement = true;
        MapLocationReveal.IsSequenceActive = true;

        // Reset animator so the base layer is clean
        Player.instance.animator.SetFloat("Velocity", 0f);
        Player.instance.animator.SetFloat("x", 0f);
        Player.instance.animator.SetFloat("y", 0f);
        Player.instance.animator.SetBool("Turning", false);

        // ── Play Timeline ──────────────────────────────────────────────────────
        bool timelineFinished = false;
        if (director != null)
        {
            System.Action<PlayableDirector> onStopped = _ => timelineFinished = true;
            director.stopped += onStopped;
            director.Play();

            // Subtitle fires after delay
            StartCoroutine(Co_TriggerSubtitleAfterDelay(subtitleDelay));

            // Auto-walk fires after delay
            StartCoroutine(Co_StartAutoWalkAfterDelay(autoWalkStartDelay));

            // Wait for Timeline to end
            yield return new WaitUntil(() => timelineFinished);
            director.stopped -= onStopped;
        }
        else
        {
            // No Timeline assigned — fire subtitle and auto-walk on a simple timer
            StartCoroutine(Co_TriggerSubtitleAfterDelay(subtitleDelay));
            yield return new WaitForSeconds(autoWalkStartDelay);
            autoWalking = true;
            StartCoroutine(Co_AutoWalk());
            // Wait a moment for the player to start running before handing off
            yield return new WaitForSeconds(1f);
        }

        // ── Hand off to knockdown sequence ─────────────────────────────────────
        autoWalking = false;

        OnHandedOffToKnockdown?.Invoke();

        if (knockdownSequence != null)
        {
            knockdownSequence.StartSequence();
        }
    }

    IEnumerator Co_TriggerSubtitleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (preSequenceSubtitle != null)
            preSequenceSubtitle.TriggerSubtitle();
    }

    IEnumerator Co_StartAutoWalkAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Optionally face the player toward a target before they start running
        if (facePlayerToward != null)
        {
            Vector3 dir = facePlayerToward.position - Player.instance.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                Player.instance.transform.rotation = Quaternion.LookRotation(dir);
        }

        // Also set AimRoot pitch/yaw offset on the VCam target
        Player.instance.playerWeaponSystem.SyncCameraYaw();
        var camTarget = Player.instance.playerWeaponSystem.CinemachineCameraTarget;
        if (camTarget != null)
            camTarget.transform.localRotation = Quaternion.Euler(autoWalkCameraPitch, autoWalkCameraYawOffset, 0f);

        // Activate the override so it wins every LateUpdate against CameraSystem
        if (cameraRotationOverride != null)
            cameraRotationOverride.isActive = true;

        autoWalking = true;
        StartCoroutine(Co_AutoWalk());
    }

    IEnumerator Co_AutoWalk()
    {
        while (autoWalking)
        {
            Vector3 forward = Player.instance.transform.forward;
            forward.y = -9.81f * Time.deltaTime; // maintain gravity
            Player.instance.controller.Move(forward * autoWalkSpeed * Time.deltaTime);

            Player.instance.animator.SetFloat("Velocity", autoWalkSpeed);
            Player.instance.animator.SetFloat("y", 1f);

            yield return null;
        }

        Player.instance.animator.SetFloat("Velocity", 0f);
        Player.instance.animator.SetFloat("y", 0f);
    }
}
