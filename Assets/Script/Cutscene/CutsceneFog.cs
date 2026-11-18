using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// Thick sandstorm fog while a cutscene timeline plays in the Game scene (user 2026-11-18: the cutscenes lost their mood when the
/// gameplay fog was thinned for the walks). Ramps Enviro's height fog up to <see cref="Density"/> when any (non-looping) timeline
/// starts and back to whatever the game had when it ends. Runs last, on top of anything else that drives the fog that frame
/// (the chases), and never lowers a value that is already thicker. No scene objects: it starts itself.
/// </summary>
[DefaultExecutionOrder(10000)]
public class CutsceneFog : MonoBehaviour
{
    /// <summary>Enviro height fog density during cutscenes (the old sandstorm value; gameplay is about 0.7).</summary>
    public static float Density = 1.9f;
    const float RampIn = 0.5f, RampOut = 2.5f;     // seconds (unscaled: subtitles can freeze the game)
    const string SceneName = "Game";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("[CutsceneFog]") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        go.AddComponent<CutsceneFog>();
    }

    float weight;                 // 0 = game fog, 1 = cutscene fog
    float under = -1f;            // the fog the game wants right now
    float written = -1f;          // what this wrote last frame (to tell it from someone else's value)
    EnviroSkyLite env;
    PlayableDirector[] directors = new PlayableDirector[0];
    float nextScan;

    /// <summary>True while a cutscene timeline is playing.</summary>
    public static bool CutsceneOn { get; private set; }

    void LateUpdate()
    {
        var e = EnviroSkyLite.instance;
        if (e != env) { env = e; under = -1f; written = -1f; weight = 0f; }
        if (env == null) return;

        CutsceneOn = SceneManager.GetActiveScene().name == SceneName && AnyCutscenePlaying();
        weight = Mathf.MoveTowards(weight, CutsceneOn ? 1f : 0f, Time.unscaledDeltaTime / (CutsceneOn ? RampIn : RampOut));

        float now = env.fogSettings.heightDensity;
        if (written < 0f || !Mathf.Approximately(now, written)) under = now;   // the game (or a chase) set its own value
        if (weight <= 0f)
        {
            if (written >= 0f) env.fogSettings.heightDensity = under;          // hand it back exactly
            written = -1f;
            return;
        }
        float v = Mathf.Lerp(under, Mathf.Max(under, Density), weight);
        env.fogSettings.heightDensity = v;
        written = v;
    }

    bool AnyCutscenePlaying()
    {
        if (Time.unscaledTime >= nextScan)
        {
            directors = FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None);
            nextScan = Time.unscaledTime + 0.5f;
        }
        foreach (var d in directors)
        {
            if (d == null || !d.isActiveAndEnabled || d.state != PlayState.Playing) continue;
            if (d.extrapolationMode == DirectorWrapMode.Loop) continue;          // ambient loops are not cutscenes
            if (d.playableAsset is TimelineAsset t && t.duration > 2.0) return true;
        }
        return false;
    }
}
