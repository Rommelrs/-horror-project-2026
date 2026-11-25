using UnityEngine;

/// <summary>
/// The park's main entrance (the gap beside the AL-SARAB PARK sign): its two white leaves stand open until Khalid owns the Hammer,
/// then they are shut with a chain. Asks the inventory itself, like StoreDoorLock, so checkpoints and loads are always right.
/// </summary>
public class ParkGate : MonoBehaviour
{
    [SerializeField] GameObject open, closed;
    float nextCheck;

    void OnEnable() => Apply();

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.5f;
        Apply();
    }

    void Apply()
    {
        bool shut = PlayerMeleeWeapon.OwnsPipe;
        if (open != null && open.activeSelf == shut) open.SetActive(!shut);
        if (closed != null && closed.activeSelf != shut) closed.SetActive(shut);
    }
}
