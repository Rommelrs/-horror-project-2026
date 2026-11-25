using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElectricityHazard : MonoBehaviour
{
    [Header("Electricity Settings")]
    [SerializeField] bool isActive = false;
    [SerializeField] GameObject electricityVisual; // Visual effect (particle system, mesh, etc.)
    [SerializeField] GameObject damageZoneObject; // GameObject with DamageOnTrigger and collider
    
    DamageOnTrigger damageZone; // Reference cached from damageZoneObject
    
    [Header("Audio")]
    [SerializeField] AudioSource electricityAudioSource;
    [SerializeField] AudioClip electricityLoopClip;
    [SerializeField] AudioClip activationClip;
    [SerializeField] AudioClip deactivationClip;

    [Header("Auto Retreat Settings")]
    [SerializeField] float retreatSpeed = 3f;
    [SerializeField] float retreatDuration = 0.5f;

    private void Start()
    {
        // Cache damage zone component
        if (damageZoneObject != null)
            damageZone = damageZoneObject.GetComponent<DamageOnTrigger>();

        // Initialize state
        if (isActive)
            ActivateElectricity();
        else
            DeactivateElectricity();
    }

    public void ActivateElectricity()
    {
        if (isActive)
            return;

        isActive = true;

        // Enable visual effect
        if (electricityVisual != null)
            electricityVisual.SetActive(true);

        // Enable damage zone GameObject
        if (damageZoneObject != null)
            damageZoneObject.SetActive(true);

        // Play activation sound
        if (electricityAudioSource != null && activationClip != null)
            electricityAudioSource.PlayOneShot(activationClip);

        // Start looping electricity sound
        if (electricityAudioSource != null && electricityLoopClip != null)
        {
            electricityAudioSource.clip = electricityLoopClip;
            electricityAudioSource.loop = true;
            electricityAudioSource.Play();
        }
    }

    public void DeactivateElectricity()
    {
        isActive = false;

        // Disable visual effect
        if (electricityVisual != null)
            electricityVisual.SetActive(false);

        // Disable damage zone GameObject
        if (damageZoneObject != null)
            damageZoneObject.SetActive(false);

        // Stop looping sound
        if (electricityAudioSource != null && electricityAudioSource.isPlaying)
        {
            electricityAudioSource.Stop();
        }

        // Play deactivation sound
        if (electricityAudioSource != null && deactivationClip != null)
            electricityAudioSource.PlayOneShot(deactivationClip);
    }

    public bool IsActive()
    {
        return isActive;
    }

    // ---- no way through while it is on, whichever way he faces (walking in backwards used to be pushed OUT the far side)
    Collider zoneCollider;
    bool sideKnown;
    float side;                 // which side of the barrier he is on (+1 / -1 along its thin axis)

    bool Zone(out Bounds b, out bool alongZ)
    {
        b = default; alongZ = true;
        if (zoneCollider == null && damageZoneObject != null) zoneCollider = damageZoneObject.GetComponent<Collider>();
        if (zoneCollider == null || !zoneCollider.gameObject.activeInHierarchy) return false;
        b = zoneCollider.bounds;
        alongZ = b.size.z <= b.size.x;      // the thin way is the way through
        return true;
    }

    void LateUpdate()
    {
        var p = Player.instance;
        if (!isActive || p == null || !Zone(out Bounds b, out bool alongZ)) { sideKnown = false; return; }
        Vector3 at = p.transform.position;
        float d = alongZ ? at.z - b.center.z : at.x - b.center.x;
        float lateral = alongZ ? Mathf.Abs(at.x - b.center.x) - b.extents.x : Mathf.Abs(at.z - b.center.z) - b.extents.z;
        float half = alongZ ? b.extents.z : b.extents.x;
        if (!sideKnown || lateral > 1.5f || Mathf.Abs(d) > half + 1.0f)
        {
            // clear of it (or it has just come on): this is his side from here
            side = d >= 0f ? 1f : -1f;
            sideKnown = true;
            return;
        }
        // into it past the middle: held at the middle on his own side
        if (d * side < 0.05f)
        {
            float back = (0.05f - d * side) * side;
            Vector3 move = alongZ ? new Vector3(0f, 0f, back) : new Vector3(back, 0f, 0f);
            var cc = p.controller;
            if (cc != null && cc.enabled) cc.Move(move); else p.transform.position += move;
        }
    }

    /// <summary>Away from the barrier, on the side he came from.</summary>
    Vector3 RetreatDirection(Player player)
    {
        if (Zone(out Bounds b, out bool alongZ))
        {
            float s = sideKnown ? side : Mathf.Sign(alongZ ? player.transform.position.z - b.center.z : player.transform.position.x - b.center.x);
            return alongZ ? new Vector3(0f, 0f, s) : new Vector3(s, 0f, 0f);
        }
        return -player.transform.forward;
    }

    // Call this when player takes damage from electricity to push them back where they came from
    public void RetreatPlayer()
    {
        if (Player.instance != null)
        {
            StartCoroutine(ApplyAutoRetreat());
        }
    }

    private IEnumerator ApplyAutoRetreat()
    {
        Player player = Player.instance;
        if (player == null)
            yield break;

        float elapsed = 0f;
        
        while (elapsed < retreatDuration)
        {
            // Move player backward (simulate pressing S key)
            if (player.GetComponent<CharacterController>() != null)
            {
                CharacterController controller = player.GetComponent<CharacterController>();
                // Away from the barrier (not "backwards": with his back to it that was straight through)
                controller.Move(RetreatDirection(player) * retreatSpeed * Time.deltaTime);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
