using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Health))]
public class ShootableButton : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("If true, can only be activated once and then disables itself.")]
    [SerializeField] bool activateOnce = true;
    [Tooltip("If true, requires a single shot to activate (sets Health to 1).")]
    [SerializeField] bool oneShot = false;

    [Header("Objects To Enable / Disable")]
    [SerializeField] GameObject[] objectsToEnable;
    [SerializeField] GameObject[] objectsToDisable;

    [Header("Events")]
    public UnityEvent OnShot;       // Fires every time the button is shot
    public UnityEvent OnActivated;  // Fires when health reaches 0 (button fully activated)

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
        Debug.Log("[ShootableButton] Initialized on: " + gameObject.name + " | HP: " + health.GetHealthValue());
    }

    private void OnDestroy()
    {
        health.OnDeath.RemoveListener(OnButtonActivated);
        health.OnDamageTaken -= OnButtonShot;
    }

    void OnButtonShot(int damage)
    {
        Debug.Log("[ShootableButton] Shot! Damage: " + damage + " | Remaining HP: " + health.GetHealthValue());
        OnShot?.Invoke();
    }

    void OnButtonActivated()
    {
        Debug.Log("[ShootableButton] Activated! activateOnce: " + activateOnce + " | already activated: " + activated);

        if (activateOnce && activated)
        {
            Debug.Log("[ShootableButton] Already activated - ignoring.");
            return;
        }
        activated = true;

        Debug.Log("[ShootableButton] Enabling " + objectsToEnable.Length + " objects, disabling " + objectsToDisable.Length + " objects.");

        foreach (var obj in objectsToEnable)
        {
            if (obj != null) { Debug.Log("[ShootableButton] Enabling: " + obj.name); obj.SetActive(true); }
            else Debug.LogWarning("[ShootableButton] objectsToEnable has a NULL entry!");
        }

        foreach (var obj in objectsToDisable)
        {
            if (obj != null) { Debug.Log("[ShootableButton] Disabling: " + obj.name); obj.SetActive(false); }
            else Debug.LogWarning("[ShootableButton] objectsToDisable has a NULL entry!");
        }

        OnActivated?.Invoke();

        if (activateOnce)
        {
            Collider coll = GetComponent<Collider>();
            if (coll != null) coll.enabled = false;
        }
    }
}
