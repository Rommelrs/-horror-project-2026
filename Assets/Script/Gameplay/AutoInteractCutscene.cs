using System.Collections;
using UnityEngine;

// Same idea as AutoPickupCutscene, but for an ImageFadeInteractable (e.g. the candy wrapper)
// instead of a note pickup: the player walks over, turns to face it, and it opens itself exactly
// like pressing Interact would. ImageFadeInteractable manages its own game-pause around opening
// and closing (fade to black, show the image, fade back in, close on a second Interact press),
// so this just waits for its onImageClosed event before handing movement back to the player.
public class AutoInteractCutscene : MonoBehaviour
{
    // Resolved via GetComponent since this script lives on the same GameObject as the
    // interactable it drives - no manual wiring needed for this one.
    ImageFadeInteractable targetInteractable;

    [Header("Auto-Walk To Position")]
    [Tooltip("Empty transform placed in the scene marking exactly where the player should stop and stand before turning to face the interactable. Leave empty to skip the walk-over and just turn in place from wherever the player triggered the sequence.")]
    [SerializeField] Transform walkToPosition;
    [SerializeField] float walkSpeed = 2.2f;
    [SerializeField] float walkAcceleration = 6f;
    [SerializeField] float walkTurnSpeed = 220f;
    [SerializeField] float arriveDistance = 0.25f;
    [Tooltip("Brief pause at idle before starting to walk, so a sprint-to-walk transition reads as an actual stop instead of blending straight into the walk.")]
    [SerializeField] float stopSettleDuration = 0.45f;
    [Tooltip("Safety cutoff - if the walk hasn't arrived by this many seconds (e.g. blocked by an obstacle), give up walking and continue the sequence from wherever the player ended up.")]
    [SerializeField] float maxWalkDuration = 5f;

    [Header("Timing")]
    [SerializeField] float turnDuration = 0.6f;
    [SerializeField] float pauseBeforeInteract = 0.3f;
    [Tooltip("Extra pause after the player closes the image before movement resumes.")]
    [SerializeField] float pauseAfterClose = 0.3f;

    bool hasTriggered = false;

    private void Awake()
    {
        targetInteractable = GetComponent<ImageFadeInteractable>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;
        if (targetInteractable == null) return;

        hasTriggered = true;
        StartCoroutine(Co_AutoInteractSequence());
    }

    IEnumerator Co_AutoInteractSequence()
    {
        Player player = Player.instance;
        PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();

        player.pauseMovement = true;
        playerMovement.SetExternalMoveInput(Vector2.zero);
        SetIdleAnimatorPose(player);

        if (walkToPosition != null)
        {
            yield return new WaitForSeconds(stopSettleDuration);
            yield return Co_WalkToPosition(player, playerMovement);
        }

        // Smoothly turn the player to face the interactable's interact point (or its own
        // transform if no interact point is assigned).
        Transform lookTarget = targetInteractable.interactPoint != null ? targetInteractable.interactPoint : targetInteractable.transform;

        Vector3 lookDirection = lookTarget.position - player.transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion startRotation = player.transform.rotation;
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

            float elapsed = 0f;
            while (elapsed < turnDuration)
            {
                elapsed += Time.deltaTime;
                player.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsed / turnDuration);
                yield return null;
            }
            player.transform.rotation = targetRotation;
        }

        yield return new WaitForSeconds(pauseBeforeInteract);

        TriggerInteraction();

        // ImageFadeInteractable pauses the game itself and only resumes on close, so wait for
        // that specific signal instead of a fixed timer.
        targetInteractable.onImageClosed.AddListener(OnInteractableClosed);
    }

    void TriggerInteraction()
    {
        targetInteractable.Interacted();

        // Mirror what InteractionHandler.OnInteractButtonPressed does on a manual press, since
        // this bypasses it entirely by calling Interacted() directly.
        Player.instance.SetLastInteractionTime(Time.unscaledTime);
        targetInteractable.OnInteracted?.Invoke();
    }

    void OnInteractableClosed()
    {
        targetInteractable.onImageClosed.RemoveListener(OnInteractableClosed);
        StartCoroutine(Co_ResumeAfterClose());
    }

    IEnumerator Co_ResumeAfterClose()
    {
        yield return new WaitForSecondsRealtime(pauseAfterClose);
        Player.instance.pauseMovement = false;
    }

    // Walks the player toward walkToPosition using the character's own facing (no strafing,
    // consistent with tank controls), driving the same animator fields PlayerMovement normally
    // would so the walk animation plays instead of a floating slide.
    IEnumerator Co_WalkToPosition(Player player, PlayerMovement playerMovement)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        float currentSpeed = walkSpeed;
        float elapsed = 0f;

        // Physics-correct braking distance (v^2 = 2*a*d) so speed reaches ~0 right as the
        // character reaches the target, instead of a fixed zone that could strand the character
        // short of arriveDistance with nothing left to close the final gap.
        float decelZone = (walkSpeed * walkSpeed) / (2f * walkAcceleration);

        while (elapsed < maxWalkDuration)
        {
            elapsed += Time.deltaTime;

            Vector3 toTarget = walkToPosition.position - player.transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance <= arriveDistance)
                break;

            Vector3 direction = toTarget / distance;
            Quaternion faceRotation = Quaternion.LookRotation(direction, Vector3.up);
            player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, faceRotation, walkTurnSpeed * Time.deltaTime);

            float targetSpeed = distance < decelZone ? 0f : walkSpeed;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, walkAcceleration * Time.deltaTime);

            SetWalkAnimatorPose(player, currentSpeed);
            cc.Move(player.transform.forward * currentSpeed * Time.deltaTime);

            yield return null;
        }

        // Bleed off any remaining speed so the walk animation eases out instead of cutting off.
        while (currentSpeed > 0.05f)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, walkAcceleration * Time.deltaTime);
            SetWalkAnimatorPose(player, currentSpeed);
            cc.Move(player.transform.forward * currentSpeed * Time.deltaTime);
            yield return null;
        }

        playerMovement.SetExternalMoveInput(Vector2.zero);
        SetIdleAnimatorPose(player);
    }

    void SetWalkAnimatorPose(Player player, float speed)
    {
        if (player.animator == null) return;
        player.animator.SetBool("Turning", false);
        player.animator.SetFloat("x", 0f);
        player.animator.SetFloat("y", 1f);
        player.animator.SetFloat("Velocity", speed);
    }

    void SetIdleAnimatorPose(Player player)
    {
        if (player.animator == null) return;
        player.animator.SetBool("Turning", false);
        player.animator.SetFloat("x", 0f);
        player.animator.SetFloat("y", 0f);
        player.animator.SetFloat("Velocity", 0f);
    }
}
