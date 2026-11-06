using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// Footsteps for whoever walks in a Timeline cutscene. Their clips have no step events, so the steps are read off the feet:
/// a foot that lifted, travelled and came back down is a step. It plays the same surface sounds as the player
/// (PlayerSound.GetFootstepsAt), so it follows wood, sand, asphalt... Adds itself to every cutscene in every scene when the
/// scene loads, and finds the actors in the Timeline's own bindings: nothing to set up. Kids (see kidNames) get lighter,
/// higher, shorter steps.
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneFootsteps : MonoBehaviour
{
    static readonly string[] kidNames = { "adel", "kid", "child" };

    // an adult's foot has to come up this far and travel this far for a step to count: a shuffle or a turn on the spot isn't one
    const float LiftAdult = 0.05f, LiftKid = 0.035f;
    const float TravelAdult = 0.16f, TravelKid = 0.1f;
    const float DownMargin = 0.03f;      // back down to within this of the lowest it has been
    const float MinStepGap = 0.16f;

    const float VolumeAdult = 1f, VolumeKid = 0.5f;
    const float PitchAdult = 1f, PitchKid = 1.4f;
    const float PitchVariation = 0.05f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < SceneManager.sceneCount; i++) OnSceneLoaded(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var d in root.GetComponentsInChildren<PlayableDirector>(true))
                if (d.playableAsset is TimelineAsset && d.GetComponent<CutsceneFootsteps>() == null)
                    d.gameObject.AddComponent<CutsceneFootsteps>();
    }

    class Foot
    {
        public Transform bone;
        public float lowest;
        public Vector3 lastPlant;
        public bool lifted;
    }

    class Actor
    {
        public Animator animator;
        public Foot left, right;
        public bool kid;
        public float nextStep;
        public AudioSource source;
    }

    PlayableDirector director;
    readonly List<Actor> actors = new List<Actor>();
    bool tracking;

    void Awake() => director = GetComponent<PlayableDirector>();

    void OnDisable() => StopTracking();

    void LateUpdate()
    {
        bool playing = director != null && director.state == PlayState.Playing && director.playableGraph.IsValid();
        if (playing && !tracking) StartTracking();
        else if (!playing && tracking) StopTracking();
        if (!tracking) return;

        foreach (var a in actors)
        {
            if (a.animator == null || !a.animator.isActiveAndEnabled) continue;
            UpdateFoot(a, a.left);
            UpdateFoot(a, a.right);
        }
    }

    void StartTracking()
    {
        tracking = true;
        actors.Clear();
        var timeline = director.playableAsset as TimelineAsset;
        if (timeline == null) return;
        var seen = new HashSet<Animator>();
        foreach (var output in timeline.outputs)
        {
            var animator = director.GetGenericBinding(output.sourceObject) as Animator;
            if (animator == null || !seen.Add(animator)) continue;
            var l = FindFoot(animator, true);
            var r = FindFoot(animator, false);
            if (l == null || r == null) continue;    // a dolly, a camera holder...

            var actor = new Actor { animator = animator, kid = IsKid(animator.name) };
            actor.left = NewFoot(l);
            actor.right = NewFoot(r);
            actor.source = NewSource(animator.transform);
            actors.Add(actor);
        }
    }

    void StopTracking()
    {
        tracking = false;
        foreach (var a in actors)
            if (a.source != null) Destroy(a.source.gameObject, 2f);
        actors.Clear();
    }

    /// <summary>The characters here have generic rigs, so the feet are found by bone name.</summary>
    public static Transform FindFoot(Animator animator, bool left)
    {
        if (animator == null) return null;
        if (animator.isHuman) return animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        string a = left ? "foot.l" : "foot.r", b = left ? "LeftFoot" : "RightFoot", c = left ? "mixamorig:LeftFoot" : "mixamorig:RightFoot";
        foreach (var t in animator.GetComponentsInChildren<Transform>(true))
            if (t.name == a || t.name == b || t.name == c) return t;
        return null;
    }

    static bool IsKid(string name)
    {
        name = name.ToLowerInvariant();
        foreach (var k in kidNames) if (name.Contains(k)) return true;
        return false;
    }

    static Foot NewFoot(Transform bone) => new Foot { bone = bone, lowest = bone.position.y, lastPlant = bone.position };

    AudioSource NewSource(Transform parent)
    {
        var go = new GameObject("CutsceneFootstepAudio");
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0.6f;              // cutscene cameras stand well away from the actors: steps stay audible
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 4f;
        s.maxDistance = 40f;
        s.dopplerLevel = 0f;
        var sound = PlayerSound.instance;
        if (sound != null && sound.Source != null) s.outputAudioMixerGroup = sound.Source.outputAudioMixerGroup;
        return s;
    }

    void UpdateFoot(Actor a, Foot f)
    {
        Vector3 p = f.bone.position;

        // the ground isn't flat everywhere: the lowest point slowly follows him up a slope, and instantly down one
        f.lowest = Mathf.Min(f.lowest + 0.05f * Time.deltaTime, p.y);

        float height = p.y - f.lowest;
        if (height > (a.kid ? LiftKid : LiftAdult)) f.lifted = true;
        if (!f.lifted || height > DownMargin) return;

        Vector3 moved = p - f.lastPlant;
        moved.y = 0f;
        if (moved.magnitude < (a.kid ? TravelKid : TravelAdult))
        {
            f.lifted = false;    // came down about where it left: he only shifted his weight
            return;
        }

        f.lifted = false;
        f.lastPlant = p;
        if (Time.time < a.nextStep) return;
        a.nextStep = Time.time + MinStepGap;
        PlayStep(a, p);
    }

    void PlayStep(Actor a, Vector3 at)
    {
        var sound = PlayerSound.instance;
        if (sound == null || a.source == null) return;
        var clips = sound.GetFootstepsAt(at + Vector3.up * 0.4f, out float surface);
        if (clips == null || clips.Length == 0) return;

        a.source.transform.position = at;
        a.source.pitch = (a.kid ? PitchKid : PitchAdult) + Random.Range(-PitchVariation, PitchVariation);
        a.source.PlayOneShot(clips[Random.Range(0, clips.Length)], sound.FootstepVolume * surface * (a.kid ? VolumeKid : VolumeAdult));
    }
}
