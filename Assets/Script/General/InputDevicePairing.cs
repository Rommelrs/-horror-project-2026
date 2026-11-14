using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

/// <summary>
/// The Player's PlayerInput pairs the keyboard and the mouse that existed when the scene started, and the whole HorrorInput
/// asset then only listens to those two devices. A mouse that reconnects (wireless mouse waking up, USB hiccup, Windows
/// re-detecting it, a second mouse) comes back as a NEW device - not paired, so aiming and attacking went dead until the
/// game restarted. Any keyboard / mouse that appears, or is the one in use when the window regains focus, is paired here.
/// </summary>
public class InputDevicePairing : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("InputDevicePairing");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<InputDevicePairing>();
    }

    void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;
    void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Enabled)
            Pair(device);
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) return;
        Pair(Keyboard.current);
        Pair(Mouse.current);
    }

    float nextCheck;

    void Update()
    {
        // cheap safety net: the keyboard / mouse being used right now is always one the controls listen to
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        Pair(Keyboard.current);
        Pair(Mouse.current);
    }

    static void Pair(InputDevice device)
    {
        if (device == null || !(device is Keyboard || device is Mouse) || !device.added) return;
        foreach (var pi in PlayerInput.all)
        {
            if (pi == null || !pi.isActiveAndEnabled || !pi.user.valid) continue;
            if (pi.currentControlScheme != null && !pi.currentControlScheme.Contains("Keyboard")) continue;
            bool paired = false;
            foreach (var d in pi.user.pairedDevices) if (d == device) { paired = true; break; }
            if (paired) continue;
            InputUser.PerformPairingWithDevice(device, pi.user);
        }
    }
}
