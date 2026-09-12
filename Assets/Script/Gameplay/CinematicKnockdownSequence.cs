using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Cinemachine;

/// <summary>
/// Place on a GameObject with a trigger collider.
/// When the player enters: runner dashes at player, glass table shatters,
/// player plays knocked down animation.
/// </summary>
public class CinematicKnockdownSequence : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The RunnerEnemy that will perform the dash.")]
    [SerializeField] RunnerEnemy runnerEnemy;
    [Tooltip("The glass table to shatter when the runner hits the player.")]
    [SerializeField] GlassBreakable glassTable;
    [Tooltip("Optional: subtitle to trigger after player is knocked down.")]
    [SerializeField] SubtitleTrigger knockdownSubtitle;

    [Header("Player Animator")]
    [Tooltip("Animator layer index for the Knockdown layer (check Layers tab, starts at 0).")]
    [SerializeField] int knockdownLayerIndex = 6;
    [Tooltip("Exact name of the KnockedDown state in the Knockdown layer.")]
    [SerializeField] string knockdownStateName = "KnockedDown";
    [Tooltip("Exact name of the StandUp state in the Knockdown layer.")]
    [SerializeField] string standUpStateName = "StandUp";

    [Header("Timing")]
    [Tooltip("How close the runner must get to player to trigger knockdown effect (meters).")]
    [SerializeField] float hitDetectionRange = 2f;
    [Tooltip("Delay between triggering knockdown and glass shattering.")]
    [SerializeField] float glassBreakDelay = 0.2f;
    [Tooltip("How long the player stays knocked down before auto-standing up. 0 = wait for player input. Ignored if Manual Stand Up is true.")]
    [SerializeField] float knockdownDuration = 4f;
    [Tooltip("Key to press to stand up (if knockdownDuration is 0). Ignored if Manual Stand Up is true.")]
    [SerializeField] KeyCode standUpKey = KeyCode.E;
    [Tooltip("If true, only ForceStandUp() can trigger stand up. Call it from a UnityEvent, Timeline signal, etc.")]
    [SerializeField] bool manualStandUp = false;

    [Header("Auto Walk")]
    [Tooltip("Speed the player auto-walks forward when sequence triggers.")]
    [SerializeField] float autoWalkSpeed = 2f;

    [Header("Push Settings")]
    [Tooltip("Where the player should end up after being pushed (place this at the glass table). Push force auto-calculated from distance.")]
    [SerializeField] Transform downedPosition;
    [Tooltip("Base push force. The actual force scales with distance so player always reaches downedPosition.")]
    [SerializeField] float pushForce = 6f;
    [Tooltip("Maximum force cap to prevent flying too fast.")]
    [SerializeField] float maxPushForce = 15f;
    [Tooltip("How long the push lasts (seconds).")]
    [SerializeField] float pushDuration = 0.4f;

    [Header("Camera")]
    [Tooltip("Drag your ThirdPersonVCam here.")]
    [SerializeField] CinemachineVirtualCamera thirdPersonVCam;
    [Tooltip("How much the camera shakes on impact.")]
    [SerializeField] float shakeIntensity = 0.15f;
    [Tooltip("How long the shake lasts.")]
    [SerializeField] float shakeDuration = 0.5f;
    [Tooltip("Camera roll angle when downed (Dutch). Try 5-12.")]
    [SerializeField] float downedDutchAngle = 8f;
    [Tooltip("How smoothly the camera rolls into the downed angle.")]
    [SerializeField] float dutchLerpSpeed = 5f;
    [Tooltip("How much the FOV increases when downed (wider = shows more = feels further back). Default camera FOV + this value.")]
    [SerializeField] float downedFOVIncrease = 15f;

    [Header("Enemy Gate")]
    [Tooltip("All these enemies must be dead before player can stand up.")]
    [SerializeField] Enemy[] enemiesToKill;
    [Tooltip("Subtitle triggered when player tries to release ADS while enemies are still alive.")]
    [SerializeField] SubtitleTrigger mustKillSubtitle;

    [Header("Optional ADS While Downed")]
    [Tooltip("If true, player can ADS while knocked down.")]
    [SerializeField] bool allowADSWhileDowned = true;
    [Tooltip("Toggle mode: click once to ADS, click again to exit. Easier for debugging.")]
    [SerializeField] bool adsToggleMode = false;
    [Tooltip("A low/prone-level Cinemachine VCam used as the ADS camera while downed.")]
    [SerializeField] CinemachineVirtualCamera downedAimCam;
    [Tooltip("How far down to lower the camera pivot when downed ADS activates (meters). Try 0.8-1.5.")]
    [SerializeField] float downedViewOffset = 1.2f;
    [Tooltip("The root of the FP hands/weapon (parent above the animator). Will be reparented to downedAimCam when downed ADS activates.")]
    [SerializeField] Transform firstPersonRoot;
    [Tooltip("Local position of the hands relative to downedAimCam. Adjust until they appear correctly in view.")]
    [SerializeField] Vector3 downedHandsLocalPosition = new Vector3(0f, -0.3f, 0.5f);

    [Header("Music")]
    [Tooltip("AudioSource to play while the player is downed. Assign a looping music clip to it.")]
    [SerializeField] AudioSource knockdownMusicSource;
    [Tooltip("How long the music fades in when the player is knocked down.")]
    [SerializeField] float musicFadeInDuration = 1f;
    [Tooltip("How long the music fades out when the player stands up.")]
    [SerializeField] float musicFadeOutDuration = 2f;

    [Header("Events")]
    public UnityEvent OnSequenceStarted;
    public UnityEvent OnPlayerKnockedDown;
    public UnityEvent OnPlayerStoodUp;

    bool hasTriggered = false;
    bool isPlayerDowned = false;
    bool enemiesCleared = false;

    bool AllEnemiesDead()
    {
        if (enemiesToKill == null || enemiesToKill.Length == 0) return true;
        foreach (var e in enemiesToKill)
            if (e != null && !e.health.IsDead) return false;
        return true;
    }
    CinemachineVirtualCamera originalAimCam;
    bool downedAimActive = false;
    bool adsToggled = false;
    bool prevIsAiming = false;
    Transform fpRootOriginalParent;
    Vector3 fpRootOriginalLocalPos;
    Quaternion fpRootOriginalLocalRot;
    float originalFOV;

    private void LateUpdate()
    {
        if (downedAimActive && firstPersonRoot != null)
            firstPersonRoot.localPosition = downedHandsLocalPosition;
    }

    private void Update()
    {
        if (!isPlayerDowned) return;

        bool aiming = allowADSWhileDowned && downedAimCam != null
            ? Player.instance.playerWeaponSystem.isAiming
            : false;

        // While not aiming: freeze animation at last frame
        if (!aiming)
            Player.instance.animator.Play(knockdownStateName, knockdownLayerIndex, 1f);

        // Enemy gate: when all enemies dead and not aiming, allow E to stand up
        if (!aiming && AllEnemiesDead())
        {
            if (!enemiesCleared) enemiesCleared = true;

            if (Input.GetKeyDown(standUpKey))
            {
                StandUp();
                return;
            }
        }
        else if (!aiming && !AllEnemiesDead())
        {
            if (Input.GetKeyDown(standUpKey) && mustKillSubtitle != null
                && !SubtitleManager.instance.IsSubtitleBusy())
            {
                mustKillSubtitle.TriggerSubtitle();
                StartCoroutine(Co_WatchSubtitleDismiss());
            }
        }

        // Always track previous aiming state
        bool aimingBeforeToggle = aiming;

        if (!allowADSWhileDowned || downedAimCam == null)
        {
            prevIsAiming = aimingBeforeToggle;
            return;
        }

        // Toggle mode: detect press (false->true transition) and flip toggle
        if (adsToggleMode)
        {
            if (aiming && !prevIsAiming)
            {
                adsToggled = !adsToggled;
                Player.instance.playerWeaponSystem.aimingOverrideForTesting = adsToggled;
            }
            aiming = adsToggled;
        }

        prevIsAiming = aimingBeforeToggle;

        if (aiming && !downedAimActive)
        {
            downedAimActive = true;
            downedAimCam.gameObject.SetActive(true);
            downedAimCam.Priority = 100;

            // Lower the camera pivot point so the view is from ground level
            var camTarget = Player.instance.playerWeaponSystem.CinemachineCameraTarget.transform;
            camTarget.localPosition += Vector3.down * downedViewOffset;

            // Disable downed FOV/Dutch on thirdPersonVCam while downedAimCam is active
            if (thirdPersonVCam != null)
            {
                thirdPersonVCam.m_Lens.Dutch = 0f;
                thirdPersonVCam.m_Lens.FieldOfView -= downedFOVIncrease;
            }

            // Reparent FP hands to downedAimCam so they stick in front of it
            if (firstPersonRoot != null)
            {
                fpRootOriginalParent = firstPersonRoot.parent;
                fpRootOriginalLocalPos = firstPersonRoot.localPosition;
                fpRootOriginalLocalRot = firstPersonRoot.localRotation;
                firstPersonRoot.SetParent(downedAimCam.transform, false);
                firstPersonRoot.localPosition = downedHandsLocalPosition;
                firstPersonRoot.localRotation = Quaternion.identity;
            }
        }
        else if (!aiming && downedAimActive)
        {
            downedAimActive = false;
            downedAimCam.Priority = 0;
            downedAimCam.gameObject.SetActive(false);

            // Restore camera pivot
            var camTarget = Player.instance.playerWeaponSystem.CinemachineCameraTarget.transform;
            camTarget.localPosition -= Vector3.down * downedViewOffset;

            // Restore downed FOV/Dutch when returning to third person view
            if (thirdPersonVCam != null)
            {
                thirdPersonVCam.m_Lens.Dutch = downedDutchAngle;
                thirdPersonVCam.m_Lens.FieldOfView += downedFOVIncrease;
            }

            // Restore FP hands to original parent and position
            if (firstPersonRoot != null)
            {
                firstPersonRoot.SetParent(fpRootOriginalParent, false);
                firstPersonRoot.localPosition = fpRootOriginalLocalPos;
                firstPersonRoot.localRotation = fpRootOriginalLocalRot;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;
        hasTriggered = true;

        StartCoroutine(Co_RunSequence());
    }

    bool autoWalking = false;

    IEnumerator Co_RunSequence()
    {
        OnSequenceStarted?.Invoke();

        // Remove ALL player control
        Player.instance.playerWeaponSystem.weaponIsEnabled = false;
        Player.instance.playerWeaponSystem.ExitOutOfAiming();
        Player.instance.playerMovement.enabled = false;
        Player.instance.pauseMovement = true; // blocks gravity/movement fallback
        MapLocationReveal.IsSequenceActive = true; // blocks M, ESC, map inputs

        // Reset animator params so base layer doesn't bleed through
        Player.instance.animator.SetFloat("Velocity", 0f);
        Player.instance.animator.SetFloat("x", 0f);
        Player.instance.animator.SetFloat("y", 0f);
        Player.instance.animator.SetBool("Turning", false);

        // Force player to auto-walk forward
        autoWalking = true;
        StartCoroutine(Co_AutoWalk());

        // Force runner into dash
        if (runnerEnemy != null)
        {
            runnerEnemy.enemyDashAttackState.DashStarted += OnDashStarted;
            runnerEnemy.stateMachine.ChangeState(runnerEnemy.enemyDashAttackState);
        }

        yield return null;
    }

    IEnumerator Co_AutoWalk()
    {
        while (autoWalking)
        {
            // Direct controller movement (bypasses pauseMovement)
            Vector3 forward = Player.instance.transform.forward;
            forward.y = -9.81f * Time.deltaTime; // apply gravity
            Player.instance.controller.Move(forward * autoWalkSpeed * Time.deltaTime);

            // Drive walk animation
            Player.instance.animator.SetFloat("Velocity", autoWalkSpeed);
            Player.instance.animator.SetFloat("y", 1f);

            yield return null;
        }
        Player.instance.animator.SetFloat("Velocity", 0f);
        Player.instance.animator.SetFloat("y", 0f);
    }

    void OnDashStarted()
    {
        if (runnerEnemy != null)
            runnerEnemy.enemyDashAttackState.DashStarted -= OnDashStarted;

        StartCoroutine(Co_WaitForHit());
    }

    IEnumerator Co_WaitForHit()
    {
        // Monitor until runner reaches player
        while (runnerEnemy != null && Player.instance != null)
        {
            float dist = Vector3.Distance(runnerEnemy.transform.position, Player.instance.transform.position);
            if (dist <= hitDetectionRange)
                break;
            yield return null;
        }

        // Runner has reached the player — trigger the sequence
        StartCoroutine(Co_KnockdownEffect());
    }

    IEnumerator Co_KnockdownEffect()
    {
        isPlayerDowned = true;
        autoWalking = false; // Stop auto-walk

        // Calculate push
        Vector3 pushDir;
        float calculatedForce;

        if (downedPosition != null)
        {
            Vector3 toTarget = downedPosition.position - Player.instance.transform.position;
            toTarget.y = 0;
            float distance = toTarget.magnitude;
            pushDir = toTarget.normalized;

            // Calculate force needed to travel that distance in pushDuration
            // distance ≈ force * pushDuration / 2 (linear deceleration), so force = distance * 2 / pushDuration
            calculatedForce = Mathf.Clamp(distance * 2f / pushDuration, pushForce, maxPushForce);
        }
        else if (runnerEnemy != null)
        {
            pushDir = (Player.instance.transform.position - runnerEnemy.transform.position);
            pushDir.y = 0;
            pushDir.Normalize();
            calculatedForce = pushForce;
        }
        else
        {
            pushDir = Player.instance.transform.forward;
            calculatedForce = pushForce;
        }

        // Face player toward the runner so animation reacts to the hit
        if (runnerEnemy != null)
        {
            Vector3 towardRunner = (runnerEnemy.transform.position - Player.instance.transform.position);
            towardRunner.y = 0;
            if (towardRunner.sqrMagnitude > 0.01f)
                Player.instance.transform.rotation = Quaternion.LookRotation(towardRunner);
        }

        StartCoroutine(Co_PushPlayer(pushDir, calculatedForce));

        // Brief pause so push starts before we lock movement
        yield return new WaitForSeconds(0.05f);
        Player.instance.pauseMovement = true;

        // Camera shake + Dutch roll on impact
        if (thirdPersonVCam != null)
            StartCoroutine(Co_CameraKnockdownEffect());

        // Activate the knockdown layer and play the animation directly
        Player.instance.animator.SetLayerWeight(knockdownLayerIndex, 1f);
        Player.instance.animator.Play(knockdownStateName, knockdownLayerIndex, 0f);

        OnPlayerKnockedDown?.Invoke();

        // Enable full weapon system (it handles FP hands/ADS normally)
        // downedAimCam override is handled in Update() via isAiming
        Player.instance.playerWeaponSystem.weaponIsEnabled = true;
        // Grant infinite ammo while downed
        Player.instance.playerWeaponSystem.infiniteAmmo = true;

        // Fade in knockdown music
        if (knockdownMusicSource != null)
        {
            knockdownMusicSource.volume = 0f;
            knockdownMusicSource.Play();
            StartCoroutine(Co_FadeMusicVolume(knockdownMusicSource, 0f, 1f, musicFadeInDuration));
        }

        // Break glass
        yield return new WaitForSeconds(glassBreakDelay);
        if (glassTable != null) glassTable.Break();

        // Trigger subtitle
        if (knockdownSubtitle != null)
            yield return new WaitForSeconds(0.5f);

        if (knockdownSubtitle != null) knockdownSubtitle.TriggerSubtitle();

        // Wait for stand up
        // If enemiesToKill is assigned, stand up is handled in Update() via enemy gate
        bool useEnemyGate = enemiesToKill != null && enemiesToKill.Length > 0;
        if (!manualStandUp && !useEnemyGate)
        {
            if (knockdownDuration > 0)
            {
                yield return new WaitForSeconds(knockdownDuration);
                StandUp();
            }
            else
            {
                yield return new WaitUntil(() => Input.GetKeyDown(standUpKey));
                StandUp();
            }
        }
        // If manualStandUp or useEnemyGate: stand up triggered externally/via Update()
    }

    void StandUp()
    {
        if (!isPlayerDowned) return;
        isPlayerDowned = false;

        // Reset ADS toggle state and force exit aiming
        adsToggled = false;
        prevIsAiming = false;
        Player.instance.playerWeaponSystem.aimingOverrideForTesting = false;
        Player.instance.playerWeaponSystem.ExitOutOfAiming();

        // Play stand up animation then deactivate the layer when done
        Player.instance.animator.Play(standUpStateName, knockdownLayerIndex, 0f);
        StartCoroutine(Co_DeactivateLayerAfterStandUp());

        // Deactivate downed aim cam and restore everything regardless of ADS state
        if (downedAimActive)
        {
            // Restore camera pivot that was lowered
            var camTarget = Player.instance.playerWeaponSystem.CinemachineCameraTarget.transform;
            camTarget.localPosition -= Vector3.down * downedViewOffset;

            // Restore FP hands parent
            if (firstPersonRoot != null && fpRootOriginalParent != null)
            {
                firstPersonRoot.SetParent(fpRootOriginalParent, false);
                firstPersonRoot.localPosition = fpRootOriginalLocalPos;
                firstPersonRoot.localRotation = fpRootOriginalLocalRot;
            }
        }

        // Always reset Dutch and FOV to original values at the very end
        if (thirdPersonVCam != null)
        {
            thirdPersonVCam.m_Lens.Dutch = 0f;
            thirdPersonVCam.m_Lens.FieldOfView = originalFOV;
        }

        downedAimActive = false;
        if (downedAimCam != null) { downedAimCam.Priority = 0; downedAimCam.gameObject.SetActive(false); }

        // Restore ALL
        MapLocationReveal.IsSequenceActive = false;
        Player.instance.pauseMovement = false;
        Player.instance.playerMovement.enabled = true;
        Player.instance.playerWeaponSystem.weaponIsEnabled = true;
        Player.instance.playerWeaponSystem.LockCameraPosition = false;
        // Restore normal ammo consumption
        Player.instance.playerWeaponSystem.infiniteAmmo = false;

        // Fade out knockdown music
        if (knockdownMusicSource != null && knockdownMusicSource.isPlaying)
            StartCoroutine(Co_FadeMusicVolume(knockdownMusicSource, knockdownMusicSource.volume, 0f, musicFadeOutDuration, stopOnComplete: true));

        OnPlayerStoodUp?.Invoke();
    }

    IEnumerator Co_CameraKnockdownEffect()
    {
        // Quick position shake
        float elapsed = 0f;
        Transform vcamTransform = thirdPersonVCam.transform;
        Vector3 originalPos = vcamTransform.localPosition;
        while (elapsed < shakeDuration)
        {
            float strength = Mathf.Lerp(shakeIntensity, 0f, elapsed / shakeDuration);
            vcamTransform.localPosition = originalPos + Random.insideUnitSphere * strength;
            elapsed += Time.deltaTime;
            yield return null;
        }
        vcamTransform.localPosition = originalPos;

        originalFOV = thirdPersonVCam.m_Lens.FieldOfView;

        float dutchElapsed = 0f;
        float dutchTime = 0.3f;
        while (dutchElapsed < dutchTime)
        {
            dutchElapsed += Time.deltaTime;
            float t = dutchElapsed / dutchTime;
            thirdPersonVCam.m_Lens.Dutch = Mathf.Lerp(0f, downedDutchAngle, t);
            thirdPersonVCam.m_Lens.FieldOfView = Mathf.Lerp(originalFOV, originalFOV + downedFOVIncrease, t);
            yield return null;
        }
        thirdPersonVCam.m_Lens.Dutch = downedDutchAngle;
        thirdPersonVCam.m_Lens.FieldOfView = originalFOV + downedFOVIncrease;
    }

    IEnumerator Co_PushPlayer(Vector3 direction, float force)
    {
        float elapsed = 0f;
        while (elapsed < pushDuration)
        {
            float currentForce = Mathf.Lerp(force, 0f, elapsed / pushDuration);
            Player.instance.controller.Move(direction * currentForce * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator Co_DeactivateLayerAfterStandUp()
    {
        // Wait for stand up animation to finish
        yield return new WaitForEndOfFrame();
        AnimatorStateInfo info = Player.instance.animator.GetCurrentAnimatorStateInfo(knockdownLayerIndex);
        yield return new WaitForSeconds(info.length);

        // Deactivate knockdown layer so base layer resumes
        Player.instance.animator.SetLayerWeight(knockdownLayerIndex, 0f);
    }

    IEnumerator Co_WatchSubtitleDismiss()
    {
        // Wait one frame for subtitle to start showing
        yield return null;

        // Watch for E press every frame to dismiss subtitle
        // yield return null works even with Time.timeScale = 0
        while (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy())
        {
            if (Input.GetKeyDown(standUpKey))
            {
                SubtitleManager.instance.ForceClose();
                yield break;
            }
            yield return null;
        }
    }

    IEnumerator Co_FadeMusicVolume(AudioSource source, float from, float to, float duration, bool stopOnComplete = false)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        source.volume = to;
        if (stopOnComplete) source.Stop();
    }

    // Call this from outside if you need to force stand up
    public void ForceStandUp() => StandUp();
}
