using System.Collections;
using UnityEngine;

/// <summary>
/// Place on a trigger volume near an InspectableItemPickup. When the player enters, movement
/// briefly pauses, the player turns to face the item, and it's automatically picked up -
/// a small scripted moment instead of requiring the player to walk up and press interact.
/// </summary>
public class AutoPickupCutscene : MonoBehaviour
{
    [Tooltip("The note/item pickup this cutscene should collect. Uses its own interactPoint as the look-at target.")]
    [SerializeField] InspectableItemPickup targetPickup;

    [Header("Auto-Walk To Position")]
    [Tooltip("Empty transform placed in the scene marking exactly where the player should stop and stand before turning to face the note. Leave empty to skip the walk-over and just turn in place from wherever the player triggered the sequence.")]
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
    [SerializeField] float pauseBeforePickup = 0.3f;
    [SerializeField] float pauseAfterPickup = 0.5f;

    bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;

        // If the note was already picked up in a previous session (destroyed by the save
        // system on load), there's nothing to do.
        if (targetPickup == null) return;

        hasTriggered = true;
        StartCoroutine(Co_AutoPickupSequence());
    }

    IEnumerator Co_AutoPickupSequence()
    {
        Player player = Player.instance;
        PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();

        player.pauseMovement = true;
        // Stop reflecting whatever direction was last held so the animator eases back to idle
        // instead of freezing mid-stride (PlayerMovement itself resets this on pause too, but
        // setting it explicitly here means the fix applies immediately, this frame).
        playerMovement.SetExternalMoveInput(Vector2.zero);
        // PlayerMovement's own blend rate is too slow to visibly settle in a fraction of a
        // second (it's tuned for many-second held-input play), so snap straight to a clean
        // idle pose here instead of waiting on that blend - otherwise a sprint-to-walk entry
        // reads as an instant cut into walking with no stop in between.
        SetIdleAnimatorPose(player);

        if (walkToPosition != null)
        {
            // Hold the idle pose for a beat so the stop actually registers before walking off.
            yield return new WaitForSeconds(stopSettleDuration);
            yield return Co_WalkToPosition(player, playerMovement);
        }

        // Smoothly turn the player to face the item's interact point (or its own transform
        // if no interact point is assigned).
        Transform lookTarget = targetPickup.interactPoint != null ? targetPickup.interactPoint : targetPickup.transform;

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

        yield return new WaitForSeconds(pauseBeforePickup);

        PickUpItem();

        // Manual pickup doesn't resume movement itself - it relies on GameManager.IsPaused
        // (set by InspectItem below) freezing everything until the player closes the
        // inspection menu. Wait for that same signal so movement resumes at the same point
        // manual pickup would, instead of resuming underneath the still-open inspection UI.
        if (ItemInspectionHandler.instance != null && ItemInspectionHandler.instance.InspectionMenuIsActive())
        {
            ItemInspectionHandler.instance.onCloseInspection += OnInspectionClosed;
        }
        else
        {
            yield return new WaitForSeconds(pauseAfterPickup);
            player.pauseMovement = false;
        }
    }

    // Walks the player toward walkToPosition using the character's own facing (no strafing,
    // consistent with tank controls), driving the same animator fields PlayerMovement normally
    // would so the walk animation plays instead of a floating slide.
    IEnumerator Co_WalkToPosition(Player player, PlayerMovement playerMovement)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        // Start at full walk speed right away rather than ramping up from 0 - the stop-settle
        // pause before this already established a clean idle pose, so easing in again here
        // just reintroduces a low-Velocity window that reads as idle while still sliding.
        float currentSpeed = walkSpeed;
        float elapsed = 0f;

        // Physics-correct braking distance (v^2 = 2*a*d) so speed reaches ~0 right as the
        // character reaches the target, instead of a fixed zone that left speed hitting 0 a
        // good ~0.2m short of arriveDistance - stranding the character there with nothing left
        // to close the final gap until the maxWalkDuration failsafe eventually forced it through.
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

            // Ease speed toward 0 on approach so the stop is smooth instead of an instant halt.
            float targetSpeed = distance < decelZone ? 0f : walkSpeed;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, walkAcceleration * Time.deltaTime);

            // PlayerMovement.HandleMovement() zeroes moveInput every frame while pauseMovement is
            // true (that's the fix for the frozen-mid-animation bug), which means moveInput can't
            // be used to drive the walk pose here - it gets wiped before HandleAnimation reads it.
            // Drive the animator directly instead, every frame, so nothing can blend it back down.
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

    void OnInspectionClosed(string itemName)
    {
        ItemInspectionHandler.instance.onCloseInspection -= OnInspectionClosed;
        StartCoroutine(Co_ResumeAfterInspectionClosed());
    }

    IEnumerator Co_ResumeAfterInspectionClosed()
    {
        yield return new WaitForSecondsRealtime(pauseAfterPickup);
        Player.instance.pauseMovement = false;
    }

    void PickUpItem()
    {
        if (targetPickup == null || targetPickup.itemToPickup == null)
            return;

        // Open the same inspection UI a manual pickup uses - this pauses the game
        // (GameManager.IsPaused) and shows the note/item exactly like pressing Interact
        // would. Closing it fires onCloseInspection, which MapLocationReveal already
        // listens for to mark the note on the map, so that keeps working unchanged.
        if (ItemInspectionHandler.instance != null)
            ItemInspectionHandler.instance.InspectItem(targetPickup.itemToPickup);

        Player.instance.inventory.AddItem(targetPickup.itemToPickup, targetPickup.itemQuantity);

        if (targetPickup.pckupClip != null)
        {
            if (SoundEffectManager.instance != null)
                SoundEffectManager.instance.PlaySFX(targetPickup.pckupClip);
            else
                AudioSource.PlayClipAtPoint(targetPickup.pckupClip, targetPickup.transform.position);
        }

        Player.instance.SetLastInteractionTime(Time.unscaledTime);

        targetPickup.OnInteracted?.Invoke();

        if (targetPickup.destroyOnInteract)
        {
            SaveablePickup saveablePickup = targetPickup.GetComponent<SaveablePickup>();
            if (saveablePickup != null)
                saveablePickup.MarkAsPickedUp();

            if (ItemInspectionHandler.instance != null)
                ItemInspectionHandler.instance.InspectableItemTriggerExit(targetPickup);

            Destroy(targetPickup.gameObject);
        }
    }
}
