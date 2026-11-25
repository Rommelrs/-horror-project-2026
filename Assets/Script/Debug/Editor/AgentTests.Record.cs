#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class AgentTests
{
    /// <summary>The two clips of the stability tutorial (Resources/Tutorials): frames to disk, ffmpeg makes the videos.</summary>
    static IEnumerator RecordStability()
    {
        while (P == null) yield return 0.2f;
        yield return 2f;
        Jump(12); yield return 0.3f;
        CameraSystem.Instance.SetFreeCamera(false);
        foreach (var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
        Jump(36); yield return 3f;            // Alley 1's gate
        P.controller.enabled = false;
        P.transform.SetPositionAndRotation(new Vector3(-176.7f, 1.5f, -6f), Quaternion.Euler(0f, 180f, 0f));
        P.controller.enabled = true;
        P.OnPlayerTelported?.Invoke();
        yield return 1.5f;
        Press(Key.Digit2); yield return 0.2f; Press(); yield return 2.5f;
        foreach (var tj in Resources.FindObjectsOfTypeAll<TaskJump>()) tj.enabled = false;
        foreach (var it in Resources.LoadAll<Item>("Items")) if (it.name == "Light Ammo") P.inventory.PickUp(it, 60);
        var ws = P.playerWeaponSystem;
        foreach (int stab in new[] { 100, 0 })
        {
            string dir = AgentTools.Dir + "rec" + stab + "/";
            if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir)) File.Delete(f); else Directory.CreateDirectory(dir);
            P.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            ws.SyncCameraYaw();
            P.playerStability.stability = stab;
            ws.EnsureLoaded(8);
            ws.aimingOverrideForTesting = true;
            yield return 2f;
            Time.captureFramerate = 24;
            int start = Time.frameCount, last = -1;
            const int total = 24 * 10;
            while (true)
            {
                int f = Time.frameCount - start;
                if (f >= total) break;
                P.playerStability.stability = stab;
                if (f != last)
                {
                    last = f;
                    ScreenCapture.CaptureScreenshot(dir + f.ToString("0000") + ".png");
                    // two shots, then the reload; whatever is left of the ten seconds he just aims
                    if (f == 60 || f == 84) Hold(false, true); else Hold(false, false);
                    if (f == 100) Press(Key.R); else if (f == 104) Press();
                }
                yield return null;
            }
            Time.captureFramerate = 0;
            yield return 1f;
            Debug.Log("T rec " + stab + " frames " + Directory.GetFiles(dir).Length + " reloading " + ws.isReloading);
            while (ws.isReloading) yield return 0.3f;
            ws.aimingOverrideForTesting = false;
            yield return 1f;
        }
    }
}
#endif
