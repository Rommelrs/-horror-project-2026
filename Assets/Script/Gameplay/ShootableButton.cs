using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Health))]
public class ShootableButton : Interactable
{
    [Header("Settings")]
    [Tooltip("If true, can only be activated once and then disables itself.")]
    [SerializeField] bool activateOnce = true;

    [Header("Objects To Enable / Disable")]
    [SerializeField] GameObject[] objectsToEnable;
    [SerializeField] GameObject[] objectsToDisable;

    [Header("Events")]
    public UnityEvent OnShot;
    public UnityEvent OnActivated;
    public UnityEvent OnReset;

    Health health;
    bool activated = false;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void Start()
    {
        health.OnDeath.AddListener(OnButtonActivated);
        health.OnDamageTaken += OnButtonShot;
    }

    private void OnDestroy()
    {
        health.OnDeath.RemoveListener(OnButtonActivated);
        health.OnDamageTaken -= OnButtonShot;
    }

    // Called when player presses E
    public override void Interacted()
    {
        base.Interacted();
        if (activated && activateOnce) return;
        Activate();
    }

    void OnButtonShot(int damage)
    {
        OnShot?.Invoke();
    }

    void OnButtonActivated()
    {
        Activate();
    }

    void Activate()
    {
        if (activateOnce && activated) return;
        activated = true;

        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(true);

        foreach (var obj in objectsToDisable)
            if (obj != null) obj.SetActive(false);

        OnActivated?.Invoke();

        if (activateOnce)
        {
            Collider coll = GetComponent<Collider>();
            if (coll != null) coll.enabled = false;
        }
    }

    // Call this from TimedLightTrigger's OnLightsOff event to reset the button
    public void ResetButton()
    {
        activated = false;

        // Restore health
        health.ResetHealth();

        // Reverse objects (disable what was enabled, enable what was disabled)
        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(false);

        foreach (var obj in objectsToDisable)
            if (obj != null) obj.SetActive(true);

        // Re-enable collider so button can be interacted with again
        Collider coll = GetComponent<Collider>();
        if (coll != null) coll.enabled = true;

        OnReset?.Invoke();
    }
}
