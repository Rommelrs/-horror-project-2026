using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Footsteps for a cutscene driven from code (no Timeline, so CutsceneFootsteps does not see it). Same idea: a foot that
/// lifted, travelled and came back down is a step, played with the player's surface sounds (PlayerSound.GetFootstepsAt).
/// Thresholds are lower than CutsceneFootsteps' so a slow shuffle and the steps of a turn on the spot are heard too.
/// Begin(animators...) when the cutscene starts, End() when it is over.
/// </summary>
public class ScriptedFootsteps : MonoBehaviour
{
    [Tooltip("A foot has to come up this far (m) ...")]
    public float lift = 0.028f;
    [Tooltip("... and travel this far (m) for a step to count.")]
    public float travel = 0.07f;
    public float downMargin = 0.022f;
    public float minStepGap = 0.18f;
    [Range(0f, 2f)] public float volume = 1f;

    class Foot { public Transform bone; public float lowest; public Vector3 lastPlant; public bool lifted; }
    class Actor { public Animator animator; public Foot left, right; public float nextStep; public AudioSource source; public float volume; }

    readonly List<Actor> actors = new List<Actor>();
    /// <summary>Steps heard so far (for tests).</summary>
    public int StepsPlayed { get; private set; }

    /// <summary>Starts listening to these characters' feet. volumeScale per actor (1 = player footstep loudness).</summary>
    public void Track(Animator animator, float volumeScale = 1f)
    {
        if (animator == null) return;
        foreach (var a in actors) if (a.animator == animator) return;
        var l = CutsceneFootsteps.FindFoot(animator, true);
        var r = CutsceneFootsteps.FindFoot(animator, false);
        if (l == null || r == null) { Debug.LogWarning("[ScriptedFootsteps] no feet found on " + animator.name); return; }
        var src = new GameObject("ScriptedFootstepAudio").AddComponent<AudioSource>();
        src.transform.SetParent(transform, false);
        src.playOnAwake = false; src.spatialBlend = 0.6f; src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 4f; src.maxDistance = 40f; src.dopplerLevel = 0f;
        var sound = PlayerSound.instance;
        if (sound != null && sound.Source != null) src.outputAudioMixerGroup = sound.Source.outputAudioMixerGroup;
        actors.Add(new Actor { animator = animator, left = NewFoot(l), right = NewFoot(r), source = src, volume = volumeScale });
    }

    public void End()
    {
        foreach (var a in actors) if (a.source != null) Destroy(a.source.gameObject, 2f);
        actors.Clear();
    }

    static Foot NewFoot(Transform bone) => new Foot { bone = bone, lowest = bone.position.y, lastPlant = bone.position };

    void LateUpdate()
    {
        foreach (var a in actors)
        {
            if (a.animator == null || !a.animator.isActiveAndEnabled) continue;
            UpdateFoot(a, a.left);
            UpdateFoot(a, a.right);
        }
    }

    void UpdateFoot(Actor a, Foot f)
    {
        Vector3 p = f.bone.position;
        f.lowest = Mathf.Min(f.lowest + 0.05f * Time.deltaTime, p.y);
        float height = p.y - f.lowest;
        if (height > lift) f.lifted = true;
        if (!f.lifted || height > downMargin) return;
        Vector3 moved = p - f.lastPlant; moved.y = 0f;
        f.lifted = false;
        if (moved.magnitude < travel) return;     // came down where it left: only a weight shift
        f.lastPlant = p;
        if (Time.time < a.nextStep) return;
        a.nextStep = Time.time + minStepGap;
        var sound = PlayerSound.instance;
        if (sound == null || a.source == null) return;
        var clips = sound.GetFootstepsAt(p + Vector3.up * 0.4f, out float surface);
        if (clips == null || clips.Length == 0) return;
        a.source.transform.position = p;
        a.source.pitch = 1f + Random.Range(-0.05f, 0.05f);
        a.source.PlayOneShot(clips[Random.Range(0, clips.Length)], sound.FootstepVolume * surface * a.volume * volume);
        StepsPlayed++;
    }

    void OnDisable() => End();
}
