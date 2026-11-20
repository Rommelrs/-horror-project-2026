#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

/// <summary>
/// Editor-only test tool: saves what the game camera sees every 'interval' seconds of game time (to judge a cutscene
/// shot by shot without watching it). FrameGrabber.Begin(folder, interval, count) in Play mode.
/// </summary>
public class FrameGrabber : MonoBehaviour
{
    string folder;
    float interval;
    int count, w, h;

    public static int Saved { get; private set; }
    public static bool Running { get; private set; }
    /// <summary>Called with each frame's number just before it is taken (to log what is going on).</summary>
    public static System.Func<string> Label;
    public static string Log = "";

    public static void Begin(string folder, float interval, int count, int width = 640, int height = 360)
    {
        var go = new GameObject("Frame Grabber (debug)");
        var g = go.AddComponent<FrameGrabber>();
        g.folder = folder; g.interval = interval; g.count = count; g.w = width; g.h = height;
        Saved = 0; Running = true; Log = "";
        System.IO.Directory.CreateDirectory(folder);
        g.StartCoroutine(g.Run());
    }

    IEnumerator Run()
    {
        var rt = new RenderTexture(w, h, 24);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        float next = Time.time;
        while (Saved < count)
        {
            yield return new WaitForEndOfFrame();
            if (Time.time < next) continue;
            next += interval;
            var cam = Camera.main;
            if (cam == null) continue;
            var old = cam.targetTexture;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "f_" + Saved.ToString("00") + ".png"), tex.EncodeToPNG());
            Log += Saved.ToString("00") + " t=" + Time.time.ToString("F2") + " fov=" + cam.fieldOfView.ToString("F0") + (Label != null ? " " + Label() : "") + "\n";
            Saved++;
        }
        rt.Release();
        Destroy(tex);
        Running = false;
        Destroy(gameObject);
    }
}

/// <summary>Editor-only: one step of the tape cutscene test per call (fresh wardrobe run -> wardrobe open -> the tape
/// taken as the pickup would, frames saved to 'folder'). Call until it says "picked".</summary>
public static class SwarmCutsceneTest
{
    static bool started;

    public static string Step(string folder, float interval = 0.5f, int count = 26)
    {
        if (!started) { started = true; StoryFlags.ResetSession(); Step4DebugJumps.FreshWardrobeRun(false); return "fresh run started"; }
        if (Step4DebugJumps.Busy) return "busy";
        InspectableItemPickup tape = null;
        foreach (var t in Object.FindObjectsByType<InspectableItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name.Contains("in wardrobe")) tape = t;
        if (tape == null) return "no tape object";
        if (!tape.gameObject.activeInHierarchy)
        {
            foreach (var o in Object.FindObjectsByType<BurnableObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                for (var t = o.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("Wardrobe Cloth Seal")) { if (o.CanUseLighter()) o.TryBurn(); return "wardrobe opened"; }
            return "wardrobe not found";
        }
        var p = Player.instance;
        p.inventory.PickUp(tape.itemToPickup, tape.itemQuantity);
        var save = tape.GetComponent<SaveablePickup>();
        if (save != null) save.MarkAsPickedUp();
        if (ItemInspectionHandler.instance != null) ItemInspectionHandler.instance.InspectableItemTriggerExit(tape);
        Object.Destroy(tape.gameObject);
        var g = Object.FindFirstObjectByType<SwarmEscapeGuide>();
        FrameGrabber.Label = () => "shot " + g.IntroShot + " k " + Player.instance.transform.position.ToString("F1");
        FrameGrabber.Begin(folder, interval, count);
        started = false;
        return "picked t=" + Time.time.ToString("F1");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => started = false;
}
#endif
