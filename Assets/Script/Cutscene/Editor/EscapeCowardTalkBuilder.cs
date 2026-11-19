using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// The swarm escape cutscene, script of 2026-11-19 (Tools > Cutscenes > Build Escape Coward Talk). Khalid alone at the
/// wall says five lines (his existing recordings, re-spaced: "This isn't normal...", "What the hell did I walk into?",
/// "Maybe...", "Maybe this is what I get.", "Maybe I died in that car crash..."), then - inserted into the timeline before
/// the moving shot (Shot11) - the coward walks up, finds him there and talks; Khalid barely answers ("Just... leave me
/// alone, will ya?"); the coward gives up and walks off; the old ending plays on after it.
/// Built on version A (SadCutsceneTimeline); version B is then rebuilt from A by EscapeCutsceneBBuilder (this root goes
/// under its "Rest Offset"). Re-runnable: the original timeline is kept in AnimationTools/escape_coward and put back first.
/// Lines live in AnimationTools/cutscene_coward/lines.tsv (ESC_*); not-yet-recorded ones are silent placeholders.
/// Khalid is a frozen mesh here (KhalidSadPose), so the shots frame him from the side / over his shoulder.
/// </summary>
public static partial class CowardMeetingBuilder
{
    const string EscTimeline = "Assets/Cutscenes/SadCutsceneTimeline.playable";
    const string EscBackup = "AnimationTools/escape_coward/SadCutsceneTimeline.original.playable";
    const string EscRootName = "Escape Coward Talk";
    // between "Maybe I died in that car crash..." (Shot10) and the slow moving shot (Shot11): everything from here on
    // moves later by the length of the talk
    const float EscAt = 58.1f;
    const string CowardWalkClip = Dir + "/Coward_Walk.anim";   // 1.32 m/s, walks 2.5 deg right of where he faces
    const float CowardWalkFix = -2.5f, CowardWalkSpeed2 = 1.32f;

    [MenuItem("Tools/Cutscenes/Build Escape Coward Talk (Game scene, rebuilds B)")]
    public static void BuildEscapeCowardTalk()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!scene.isLoaded) { Debug.LogError("[EscapeTalk] open the Game scene first"); return; }
        if (EditorApplication.isPlaying) { Debug.LogError("[EscapeTalk] stop Play mode first"); return; }

        // ---- the timeline as it was before this builder (re-runs start from it)
        if (!File.Exists(EscBackup)) { Directory.CreateDirectory(Path.GetDirectoryName(EscBackup)); File.Copy(EscTimeline, EscBackup); }
        else { File.Copy(EscBackup, EscTimeline, true); AssetDatabase.ImportAsset(EscTimeline, ImportAssetOptions.ForceUpdate); }
        var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(EscTimeline);

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        Transform a = all.FirstOrDefault(t => t.name == "ActionSequenceCutscene" && t.parent != null && t.parent.name == "Cutscenes");
        if (a == null) { Debug.LogError("[EscapeTalk] ActionSequenceCutscene not found"); return; }
        var d = a.GetComponentInChildren<PlayableDirector>(true);
        d.playableAsset = tl;
        CleanBindings(d);
        var meeting = all.FirstOrDefault(t => t.name == "CowardMeeting1");
        LoadLines();

        var old = a.Find(EscRootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(EscRootName).transform;
        root.SetParent(a, false);
        root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity;

        // ---- where everyone is: Khalid's frozen pose against the north wall, facing the alley (+z)
        var sad = a.Find("KhalidSadPose").GetComponent<Renderer>().bounds;
        Vector3 f = Vector3.forward, e = Vector3.right;
        Vector3 K = new Vector3(sad.center.x, GroundY(sad.center + f * 0.8f), sad.center.z);
        Vector3 khalidHead = new Vector3(sad.center.x, sad.max.y - 0.12f, sad.center.z);
        Vector3 C = K + f * 2.3f + e * 0.9f; C.y = GroundY(C);           // "stops a few steps away"
        Vector3 kc = Flat(C - K).normalized, perp = Vector3.Cross(Vector3.up, kc);
        float cYaw = YawTo(C, K);
        // he comes along the alley: from the east if it's clear, else the west, else straight across
        Vector3 S = C;
        foreach (var dir in new[] { e, -e, f })
        {
            Vector3 s = C + dir * 4.6f; s.y = GroundY(s);
            if (!Physics.Linecast(s + Vector3.up * 0.7f, C + Vector3.up * 0.7f, ~0, QueryTriggerInteraction.Ignore)) { S = s; break; }
        }
        if (S == C) { Debug.LogWarning("[EscapeTalk] no clear way in for the coward - coming from the east anyway"); S = C + e * 4.6f; S.y = GroundY(S); }
        float inYaw = YawTo(S, C), outYaw = YawTo(C, S);
        float arrive = Vector3.Distance(Flat(S), Flat(C)) / CowardWalkSpeed;

        // ---- the beats (seconds after EscAt): silences where the script says Khalid doesn't answer
        float L(string id) => LineLength(id);
        float c1 = arrive + 0.7f;
        float c2 = c1 + L("ESC_C1") + 1.3f;                // quieter
        float c3 = c2 + L("ESC_C2") + 2.0f;                // Khalid doesn't answer
        float c4 = c3 + L("ESC_C3") + 1.0f;
        float c5 = c4 + L("ESC_C4") + 2.0f;                // Khalid stays silent
        float c6 = c5 + L("ESC_C5") + 1.8f;                // no answer
        float k6 = c6 + L("ESC_C6") + 1.2f;                // "Just... leave me alone, will ya?"
        float c7 = k6 + L("ESC_K6") + 1.0f;
        float leave = c7 + L("ESC_C7") + 0.8f;
        const float walkOff = 4.0f;
        float G = leave + 0.3f + walkOff;

        // ---- Khalid alone: the old lines out (and their signal subtitles), his five re-spaced on the same track
        var voiceTrack = tl.GetOutputTracks().OfType<AudioTrack>().First(t => t.GetClips().Any(c => c.displayName == "VoiceLine2"));
        foreach (var c in voiceTrack.GetClips().ToList())
            if (c.displayName != "VoiceLine1") voiceTrack.DeleteClip(c);
        foreach (var t in tl.GetOutputTracks())
            foreach (var m in t.GetMarkers().ToList())
                if (m is SignalEmitter se && se.asset != null && se.asset.name.StartsWith("TriggerSubtitle")) t.DeleteMarker(m);

        // ---- room for the talk
        InsertTime(tl, EscAt, G);

        var voiceObj = d.GetGenericBinding(voiceTrack) as Component;
        if (voiceObj != null && voiceObj.GetComponent<CutsceneSubtitleReceiver>() == null) voiceObj.gameObject.AddComponent<CutsceneSubtitleReceiver>();
        if (d.GetComponent<CutsceneSubtitleReceiver>() == null) d.gameObject.AddComponent<CutsceneSubtitleReceiver>();
        Say(tl, voiceTrack, voiceTrack, "ESC_K1", 37.7f, voiceTrack);   // Shot6
        Say(tl, voiceTrack, voiceTrack, "ESC_K2", 42.9f, voiceTrack);   // Shot8
        Say(tl, voiceTrack, voiceTrack, "ESC_K3", 47.6f, voiceTrack);   // Shot9
        Say(tl, voiceTrack, voiceTrack, "ESC_K4", 51.2f, voiceTrack);
        Say(tl, voiceTrack, voiceTrack, "ESC_K5", 55.0f, voiceTrack);   // Shot10

        // ---- the coward
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/CowardNPC.prefab");
        var coward = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        coward.name = "Escape Coward";
        coward.transform.SetPositionAndRotation(S, Quaternion.Euler(0f, inYaw, 0f));
        coward.SetActive(false);
        var cAnim = coward.GetComponentInChildren<Animator>(true);
        var cHead = cAnim.GetBoneTransform(HumanBodyBones.Head) ?? coward.transform;

        var vo = meeting != null ? meeting.GetComponentsInChildren<AudioSource>(true) : new AudioSource[0];
        var voGroup = vo.FirstOrDefault(s => s.name.Contains("Coward") && s.name.StartsWith("VO"))?.outputAudioMixerGroup;
        var sfxGroup = vo.FirstOrDefault(s => s.name.StartsWith("SFX"))?.outputAudioMixerGroup;
        var voC = Source(cHead, "VO Coward (escape)", cHead.position, true, 1f, voGroup); voC.transform.localPosition = Vector3.zero; Voice(voC);
        var voK = Source(root, "VO Khalid (escape)", khalidHead, true, 1f, voGroup); Voice(voK);
        var steps = Source(root, "SFX steps (coward)", C, true, 0.45f, sfxGroup);

        // ---- cameras
        var cams = Child(root, "Cameras");
        Vector3 mid = (K + C) * 0.5f;
        Vector3 Up(Vector3 p, float h) => new Vector3(p.x, K.y + h, p.z);
        // wide from the alley: him slumped at the wall, the coward coming along it (and going again)
        var vWide = Vcam(cams, "Esc_Wide", Up(K + f * 5.6f - perp * 1.8f, 1.75f), Up(mid, 0.9f), 46f);
        // low by Khalid's head, up at the coward
        var vCowardLow = Vcam(cams, "Esc_Coward_Low", Up(K + kc * 0.8f + perp * 0.45f, 1.05f), cHead.position, 38f, cHead, 0.55f, null, 0f, 0.5f, 0.42f);
        // down at Khalid from beside the coward
        var vKhalid = Vcam(cams, "Esc_Khalid_CU", Up(C - kc * 0.9f + perp * 0.55f, 1.45f), khalidHead, 40f);
        // over Khalid's shoulder at the coward
        var vOtsK = Vcam(cams, "Esc_OTS_Khalid", Up(K - perp * 0.6f - kc * 0.05f, 1.2f), cHead.position, 40f, cHead, 0.62f, null, 0f, 0.5f, 0.42f);
        // over the coward's shoulder at Khalid
        var vOtsC = Vcam(cams, "Esc_OTS_Coward", Up(C + kc * 0.6f + perp * 0.45f, 1.6f), khalidHead, 40f);
        // the two of them from the side
        Vector3 sideDir = !Physics.Linecast(Up(mid, 1.3f), Up(mid - perp * 3.0f, 1.3f), ~0, QueryTriggerInteraction.Ignore) ? -perp : perp;
        var vSide = Vcam(cams, "Esc_Side", Up(mid + sideDir * 3.0f, 1.3f), Up(mid, 0.95f), 44f);

        float T(float t) => EscAt + t;
        var brain = Object.FindFirstObjectByType<CinemachineBrain>();
        var cm = tl.CreateTrack<CinemachineTrack>(null, "Cameras (coward talk)");
        d.SetGenericBinding(cm, brain);
        var said = new Dictionary<string, Spoken>();
        var kTrack = AudioTrackFor(tl, d, "VO Khalid (coward talk)", voK);
        var cTrack = AudioTrackFor(tl, d, "VO Coward (coward talk)", voC);
        foreach (var (id, at) in new[] { ("ESC_C1", c1), ("ESC_C2", c2), ("ESC_C3", c3), ("ESC_C4", c4), ("ESC_C5", c5), ("ESC_C6", c6), ("ESC_K6", k6), ("ESC_C7", c7) })
            said[id] = Say(tl, kTrack, cTrack, id, T(at));
        float End(string id) => said[id].end;
        float c7b = said["ESC_C7"].phrases.Count > 1 ? said["ESC_C7"].phrases[1] : said["ESC_C7"].start + 2.8f;

        Shot(cm, d, vWide, T(0f), T(c1) - 0.2f);                                   // footsteps; he comes along the alley
        Shot(cm, d, vCowardLow, T(c1) - 0.2f, T(c2) - 0.2f);                       // "...You actually came out."
        Shot(cm, d, vOtsK, T(c2) - 0.2f, End("ESC_C2") + 0.4f);                    // "What happened to you...?"
        Shot(cm, d, vKhalid, End("ESC_C2") + 0.4f, T(c3) - 0.2f);                  // he doesn't answer
        Shot(cm, d, vCowardLow, T(c3) - 0.2f, T(c4) - 0.2f);                       // "Look at yourself, man..."
        Shot(cm, d, vSide, T(c4) - 0.2f, End("ESC_C4") + 0.4f);                    // "I know a way out..."
        Shot(cm, d, vKhalid, End("ESC_C4") + 0.4f, T(c5) - 0.2f);                  // he stays silent
        Shot(cm, d, vOtsK, T(c5) - 0.2f, End("ESC_C5") + 0.3f);                    // "...still looking for your brother...?"
        Shot(cm, d, vKhalid, End("ESC_C5") + 0.3f, T(c6) - 0.2f);                  // no answer
        Shot(cm, d, vCowardLow, T(c6) - 0.2f, T(k6) - 0.2f);                       // "After everything you've seen?"
        Shot(cm, d, vOtsC, T(k6) - 0.2f, T(c7) - 0.2f);                            // "Just... leave me alone, will ya?"
        Shot(cm, d, vCowardLow, T(c7) - 0.2f, c7b - 0.2f);                         // "You're the most stubborn guy..."
        Shot(cm, d, vSide, c7b - 0.2f, T(leave) + 0.3f);                           // "...Fine. Do whatever you want."
        Shot(cm, d, vWide, T(leave) + 0.3f, T(G));                                 // and off he goes

        // ---- his body: in along the alley (the wary walk), fidgeting / talking in front of him, off again on the cut
        var cw = tl.CreateTrack<AnimationTrack>(null, "Coward (coward talk)");
        cw.trackOffset = TrackOffset.ApplyTransformOffsets;
        d.SetGenericBinding(cw, cAnim);
        var walkIn = cw.CreateClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(CarefulWalk));
        walkIn.start = T(0f); walkIn.duration = arrive + 0.2f;
        Offsets(walkIn, S, inYaw + WalkHeadingFix, false);
        Chain(cw, "Nervously_Look_Around", T(arrive) - 0.1f, T(c3) + 0.1f, C, cYaw);
        Chain(cw, "Talking_TwoHands", T(c3) - 0.3f, End("ESC_C4") + 0.3f, C, cYaw);     // "Look at yourself... Come with me."
        Chain(cw, "Nervously_Look_Around", End("ESC_C4"), T(c7) + 0.1f, C, cYaw);
        Chain(cw, "Talking_TwoHands", T(c7) - 0.3f, T(leave) + 0.32f, C, cYaw);        // "...Fine. Do whatever you want."
        var walkOut = cw.CreateClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(CowardWalkClip));
        walkOut.start = T(leave) + 0.3f; walkOut.duration = walkOff;                       // turned on the cut to the wide
        Offsets(walkOut, C, outYaw + CowardWalkFix, false);
        BlendTrack(cw);
        var act = tl.CreateTrack<ActivationTrack>(null, "Escape Coward active");
        var ac = act.CreateDefaultClip(); ac.start = T(0f) - 0.05f; ac.duration = G + 0.05f;
        act.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
        d.SetGenericBinding(act, coward);

        // footsteps in the sand, coming and going
        var sfx = AudioTrackFor(tl, d, "SFX steps (coward talk)", steps);
        for (int i = 0; 0.3f + i * CarefulStep < arrive; i++) Sound(sfx, "Assets/Audio/Footsteps/Sand/Sand" + (i % 4 + 1) + ".mp3", T(0.3f + i * CarefulStep));
        for (int i = 0; 0.25f + i * 0.55f < walkOff; i++) Sound(sfx, "Assets/Audio/Footsteps/Sand/Sand" + ((i + 1) % 4 + 1) + ".mp3", T(leave) + 0.55f + i * 0.55f);

        EditorUtility.SetDirty(tl);
        EditorUtility.SetDirty(d);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[EscapeTalk] built on A: talk " + G.ToString("F1") + " s at " + EscAt + " (timeline now " + tl.duration.ToString("F1") + " s), Khalid " + K +
                  ", coward from " + S + " to " + C);

        // ---- and B from A
        EscapeCutsceneBBuilder.Build();
        ApproachInB(C, S, root.position);
    }

    /// <summary>B is A moved 23 m west (outside the Block 3 cloth): the same way in can run into the cloth / a wall there.
    /// Picks his way in again where B is and re-aims the walk in / out on B's timeline.</summary>
    static void ApproachInB(Vector3 cA, Vector3 sA, Vector3 rootA)
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        var talk = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                        .FirstOrDefault(t => t.name == EscRootName && t.GetComponentInParent<PlayableDirector>(true) == null && t.parent != null && t.parent.name.StartsWith("Rest Offset"));
        if (talk == null) { Debug.LogWarning("[EscapeTalk] B's talk not found"); return; }
        Vector3 shift = talk.position - rootA;
        Vector3 c = cA + shift, s = sA + shift;
        bool Clear(Vector3 from) => !Physics.Linecast(from + Vector3.up * 0.7f, c + Vector3.up * 0.7f, ~0, QueryTriggerInteraction.Ignore);
        if (Clear(s)) { Debug.Log("[EscapeTalk] B: same way in as A"); return; }
        Vector3 pick = s;
        foreach (var dir in new[] { Vector3.left, Vector3.right, Vector3.forward })
        {
            Vector3 p = c + dir * 4.6f; p.y = GroundY(p);
            if (Clear(p)) { pick = p; break; }
        }
        if (pick == s) { Debug.LogWarning("[EscapeTalk] B: no clear way in"); return; }

        var b = talk.parent.parent;
        var d = b.GetComponentInChildren<PlayableDirector>(true);
        var tl = (TimelineAsset)d.playableAsset;
        var track = tl.GetOutputTracks().OfType<AnimationTrack>().First(t => t.name == "Coward (coward talk)");
        var clips = track.GetClips().OrderBy(x => x.start).ToList();
        Offsets(clips.First(), pick - talk.position, YawTo(pick, c) + WalkHeadingFix, false);   // in the parent's space
        Offsets(clips.Last(), c - talk.position, YawTo(c, pick) + CowardWalkFix, false);
        var coward = talk.Find("Escape Coward");
        coward.SetPositionAndRotation(pick, Quaternion.Euler(0f, YawTo(pick, c), 0f));
        EditorUtility.SetDirty(tl);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[EscapeTalk] B: the coward comes from " + pick + " (A's way in is blocked there)");
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static float GroundY(Vector3 p)
    {
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            return h.point.y;
        return p.y;
    }

    /// <summary>Pushes everything from 'at' on later by 'gap': clips and markers after it move, clips across it get longer,
    /// recorded (infinite) clips have their keys after it moved.</summary>
    static void InsertTime(TimelineAsset tl, double at, double gap)
    {
        const double eps = 1e-4;
        var tracks = tl.GetOutputTracks().ToList();
        if (tl.markerTrack != null && !tracks.Contains(tl.markerTrack)) tracks.Add(tl.markerTrack);
        foreach (var t in tracks)
        {
            foreach (var c in t.GetClips().ToList())
            {
                if (c.start >= at - eps) c.start += gap;
                else if (c.end > at + eps) c.duration += gap;
            }
            foreach (var m in t.GetMarkers().ToList())
                if (m.time >= at - eps) m.time += gap;
            if (t is AnimationTrack an && an.infiniteClip != null)
            {
                foreach (var b in AnimationUtility.GetCurveBindings(an.infiniteClip))
                {
                    var curve = AnimationUtility.GetEditorCurve(an.infiniteClip, b);
                    var keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++) if (keys[i].time >= at - eps) keys[i].time += (float)gap;
                    curve.keys = keys;
                    AnimationUtility.SetEditorCurve(an.infiniteClip, b, curve);
                }
                EditorUtility.SetDirty(an.infiniteClip);
            }
            EditorUtility.SetDirty(t);
        }
    }

    // cross-fades over each overlap on one track (FixBlends does every track - the timeline's own ones keep theirs)
    static void BlendTrack(TrackAsset track)
    {
        var clips = track.GetClips().OrderBy(c => c.start).ToList();
        foreach (var c in clips) { c.blendInDuration = 0; c.blendOutDuration = 0; }
        for (int i = 1; i < clips.Count; i++)
        {
            double overlap = clips[i - 1].end - clips[i].start;
            if (overlap <= 0) continue;
            clips[i].blendInDuration = overlap;
            clips[i - 1].blendOutDuration = overlap;
        }
    }

    // bindings left behind by tracks that no longer exist (an earlier build of this)
    static void CleanBindings(PlayableDirector d)
    {
        var so = new SerializedObject(d);
        var b = so.FindProperty("m_SceneBindings");
        for (int i = b.arraySize - 1; i >= 0; i--)
            if (b.GetArrayElementAtIndex(i).FindPropertyRelative("key").objectReferenceValue == null) b.DeleteArrayElementAtIndex(i);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
