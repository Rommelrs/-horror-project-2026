using UnityEngine;

/// <summary>
/// For a trigger that must fire again every time the world is restored (Continue after dying, Load) until the player has moved past
/// it, and then NEVER come back: e.g. the Alley 2 trigger that starts the ambush. Put this on the trigger object itself.
///
///  - The trigger is NOT saved as "already fired" (that is what used to disable its collider after every restore, so the ambush never
///    started again): do not put a SaveableTrigger on the trigger object, and nothing reads a "fired" flag.
///  - It is retired when Retire() is called (wire it to the events of whatever means the player has moved on: the MusicStop trigger,
///    the note being read...) or when any of the progress conditions below is already true. Retiring switches its collider and
///    scripts off and records that in 'retiredFlag', a SaveableTrigger on a SEPARATE child object with its own UniqueID, so saves and
///    checkpoints made afterwards remember it.
///  - Once retired it stays retired: the collider and scripts are forced off again every frame, and when the object is re-enabled, in
///    case anything switches them back on.
/// </summary>
public class RetireTriggerOnProgress : MonoBehaviour
{
    [Header("What to switch off")]
    [SerializeField] Collider triggerCollider;
    [Tooltip("e.g. the OnPlayerTrigger on this object.")]
    [SerializeField] Behaviour[] triggerScripts;

    [Header("Saved record of 'retired'")]
    [Tooltip("A SaveableTrigger (with its own UniqueID) on a separate child object. Not on the trigger object itself: OnPlayerTrigger would read it as 'already fired'.")]
    [SerializeField] SaveableTrigger retiredFlag;

    [Header("Retire for good once any of these is true (checked on load and every moment)")]
    [Tooltip("Saved triggers that mean the player has moved on (e.g. the MusicStop trigger).")]
    [SerializeField] SaveableTrigger[] retireWhenTriggered;
    [Tooltip("Saved switches that mean the player has moved on (e.g. the note's switch).")]
    [SerializeField] SaveableSwitch[] retireWhenActivated;
    [SerializeField, Min(0.05f)] float recheckSeconds = 0.5f;

    public bool IsRetired { get; private set; }

    float nextCheckAt;

    void Awake()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<Collider>();
    }

    void OnEnable()
    {
        if (IsRetired) Enforce();
    }

    void Start()
    {
        // before anything can fire: the saved state was restored just before Start() runs
        if (ConditionsMet()) Retire();
    }

    void Update()
    {
        if (IsRetired)
        {
            Enforce();                      // nothing may bring it back
            return;
        }
        if (Time.unscaledTime < nextCheckAt) return;
        nextCheckAt = Time.unscaledTime + recheckSeconds;
        if (ConditionsMet()) Retire();
    }

    /// <summary>The player has moved on: this trigger is finished for good (and that is remembered in saves and checkpoints).</summary>
    public void Retire()
    {
        if (!IsRetired)
        {
            IsRetired = true;
            Debug.Log("[RetireTrigger] '" + name + "' retired for good");
        }
        Enforce();
        if (retiredFlag != null) retiredFlag.MarkAsTriggered();   // idempotent
    }

    void Enforce()
    {
        if (triggerCollider != null && triggerCollider.enabled) triggerCollider.enabled = false;
        if (triggerScripts != null)
            foreach (Behaviour b in triggerScripts)
                if (b != null && b.enabled) b.enabled = false;
    }

    bool ConditionsMet()
    {
        if (retiredFlag != null && retiredFlag.WasAlreadyTriggered()) return true;
        if (retireWhenTriggered != null)
            foreach (SaveableTrigger t in retireWhenTriggered)
                if (t != null && t.WasAlreadyTriggered()) return true;
        if (retireWhenActivated != null)
            foreach (SaveableSwitch s in retireWhenActivated)
                if (s != null && s.IsActivated()) return true;
        return false;
    }
}
