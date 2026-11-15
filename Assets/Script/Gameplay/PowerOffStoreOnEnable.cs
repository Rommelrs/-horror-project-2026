using UnityEngine;

/// <summary>On the after-store cutscene: the moment it starts, the store behind Khalid loses its power.</summary>
public class PowerOffStoreOnEnable : MonoBehaviour
{
    void OnEnable()
    {
        if (Application.isPlaying && StoreInteriorPowerOff.Instance != null) StoreInteriorPowerOff.Instance.PowerOff();
    }
}
