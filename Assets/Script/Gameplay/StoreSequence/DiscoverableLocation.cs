using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Marks an important place the player has to get to know before the store sequence starts (cash register, fuse box,
/// storage-room door ...). It counts as discovered when the player interacts with it or looks straight at it for a moment.
/// Interaction is picked up automatically from an Interactable / DoorInteractable on the same object; otherwise call
/// MarkInteracted() from a UnityEvent.
/// </summary>
public class DiscoverableLocation : MonoBehaviour
{
    [SerializeField] string locationId = "Location";
    [Tooltip("The point the player has to look at. Defaults to this object.")]
    [SerializeField] Transform lookTarget;

    [Header("Seen detection")]
    [SerializeField] bool detectBySight = true;
    [SerializeField] float seeDistance = 6f;
    [Tooltip("Half angle of the cone around the camera centre that counts as 'looking at it'.")]
    [SerializeField] float seeAngle = 30f;
    [SerializeField] float dwellSeconds = 0.8f;

    [Header("Events")]
    public UnityEvent onDiscovered;

    public string LocationId => locationId;
    /// <summary>Player interacted with it (opened the menu, tried the door, inspected the box).</summary>
    public bool Interacted { get; private set; }
    /// <summary>Player clearly looked at it.</summary>
    public bool Seen { get; private set; }
    public bool Discovered => Interacted || Seen;

    float dwell;
    bool announced;

    void Awake()
    {
        var interactable = GetComponent<Interactable>();
        if (interactable != null) interactable.OnInteracted.AddListener(MarkInteracted);

        var door = GetComponent<DoorInteractable>();
        if (door != null) door.OnInteracted.AddListener(MarkInteracted);
    }

    public void MarkInteracted()
    {
        Interacted = true;
        Announce("interacted");
    }

    public void MarkSeen()
    {
        Seen = true;
        Announce("seen");
    }

    void Announce(string how)
    {
        if (announced) return;
        announced = true;
        Debug.Log("[StoreSequence] discovered '" + locationId + "' (" + how + ")");
        onDiscovered?.Invoke();
    }

    /// <summary>Called every frame by StoreSequenceManager while this location is still undiscovered.</summary>
    public void TickSight(Camera cam, float dt)
    {
        if (!detectBySight || Seen || cam == null) return;

        Transform target = lookTarget != null ? lookTarget : transform;
        Vector3 to = target.position - cam.transform.position;
        float dist = to.magnitude;
        if (dist > seeDistance || dist < 0.01f) { dwell = 0f; return; }
        if (Vector3.Angle(cam.transform.forward, to) > seeAngle) { dwell = 0f; return; }

        // something in the way?  (triggers and the player's own colliders do not count)
        if (Physics.Linecast(cam.transform.position, target.position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            bool hitSelf = hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(target);
            bool hitPlayer = hit.collider.CompareTag("Player") || hit.collider.transform.root.CompareTag("Player");
            bool closeEnough = hit.distance >= dist - 1.0f;
            if (!hitSelf && !hitPlayer && !closeEnough) { dwell = 0f; return; }
        }

        dwell += dt;
        if (dwell >= dwellSeconds) MarkSeen();
    }
}
