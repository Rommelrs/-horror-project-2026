using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// Tools > Cutscenes > Build Escape Cutscene B (Game scene). User 2026-11-18: when Khalid takes the duct tape BEFORE he has
/// cut the Block 3 cloth, the swarm escape plays a version B: the same cutscene and lines, but the walk is out on the street
/// (the north-south road at x -109) instead of down the walled lane, and he rests against the wall just OUTSIDE the Block 3
/// cloth (west of it) instead of inside Block 3. The game carries on from there (B's own spawn point).
///
/// Version B = a copy of ActionSequenceCutscene with its own copy of the timeline (SadCutsceneTimeline_B):
///  - shots 1-3 (the escape) unchanged; Shot4 follows him running down the street (no gate sounds);
///  - the walk (holder 4 + its follow camera Shot5) sits under "Walk Offset (street)", moved onto the road;
///  - the rest (sad pose, Shot6-12, holder 5, SpawnPoint) sits under "Rest Offset (outside the cloth)", moved 23 m west along
///    the same compound wall, so every close shot keeps its framing;
///  - Shot7 (a cutaway down the Block 3 corridor, now behind the cloth) looks down the empty street instead;
///  - Shot11 (a dolly east down the Block 3 corridor) dollies west along the cross street, over the parked car.
/// The cinder-block door now calls EscapeCutsceneChooser.Play, which picks A or B by the Block 3 cloth. Safe to run again.
/// </summary>
public static class EscapeCutsceneBBuilder
{
    const string TimelineA = "Assets/Cutscenes/SadCutsceneTimeline.playable";
    const string TimelineB = "Assets/Cutscenes/SadCutsceneTimeline_B.playable";
    const string NameB = "ActionSequenceCutscene B";
    const string ChooserName = "Escape Cutscene Chooser";

    // where the walk starts on the street (A starts at (-47.10, 0.84, -42.28) in the lane); the road is 0.65 m above the sand
    static readonly Vector3 WalkA = new Vector3(-47.10f, 0.84f, -42.28f);
    static readonly Vector3 WalkB = new Vector3(-107.0f, 1.49f, -44.0f);
    // Shot4 for B (user: the close shot behind the rock where he ran off into the fog -> him running down the road): he runs
    // south down the east lane of the same street (cars are parked in the west lane) from RunFrom to RunTo for the whole shot,
    // straight into the walk, with Shot4 following just ahead of him, low, looking back at him. No gate sounds on the road.
    static readonly Vector3 RunFrom = new Vector3(-107.0f, 1.49f, -4.0f);
    static readonly Vector3 RunTo = new Vector3(-107.0f, 1.49f, -43.6f);
    static readonly Vector3 Shot4FollowOffset = new Vector3(1.8f, 1.35f, -4.4f);
    // the rest: same wall (the south compound's north wall), just west of the cloth (x -82.2) instead of inside Block 3
    static readonly Vector3 RestShift = new Vector3(-23.15f, 0f, 0f);
    // Shot7 for B: on the road, looking up the empty street at the parked car in the fog
    static readonly Vector3 Shot7Pos = new Vector3(-111.0f, 2.1f, -76.0f);
    static readonly Vector3 Shot7Euler = new Vector3(2f, 12f, 356f);
    // Shot11 for B (user: over the cars in the road): the same slow dolly (its curve is in the camera's tilted frame, so the
    // tilt stays 26.67 to keep it level), west along the cross street at z -61, ending on the car parked in the junction
    static readonly Vector3 Shot11Start = new Vector3(-92.0f, 3.40f, -61.4f);
    static readonly Vector3 Shot11Euler = new Vector3(26.67f, 270f, 356f);

    [MenuItem("Tools/Cutscenes/Build Escape Cutscene B (Game scene)")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!scene.isLoaded) { Debug.LogError("[EscapeB] open the Game scene first"); return; }
        if (EditorApplication.isPlaying) { Debug.LogError("[EscapeB] stop Play mode first"); return; }
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        Transform a = all.FirstOrDefault(t => t.name == "ActionSequenceCutscene");
        Transform door = all.FirstOrDefault(t => t.name == "DoorInteractableBlocks");
        Transform actionSeq = all.FirstOrDefault(t => t.name == "Action Sequence" && t.parent != null && t.parent.name == "Alley 3 Gameplay");
        var block3 = all.Select(t => t.GetComponent<BurnableObstacle>()).FirstOrDefault(b => b != null && b.name == "ClothBlockage");
        if (a == null || door == null || actionSeq == null || block3 == null) { Debug.LogError("[EscapeB] ActionSequenceCutscene / DoorInteractableBlocks / ClothBlockage not found"); return; }

        // the Al-Sarab Street redirect is replaced by version B
        foreach (var n in new[] { "Escape Ending Redirect (prototype)", "EscapeEnd_AlSarabStreet" })
        {
            var old = actionSeq.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var oldB = all.FirstOrDefault(t => t != null && t.name == NameB);
        if (oldB != null) Object.DestroyImmediate(oldB.gameObject);

        var dirA = a.GetComponentInChildren<PlayableDirector>(true);
        var trigA = dirA.GetComponent<CutsceneTrigger>();
        CleanCompleteEvent(trigA);

        // ---- copy the timeline + the whole cutscene
        AssetDatabase.DeleteAsset(TimelineB);
        AssetDatabase.CopyAsset(TimelineA, TimelineB);
        var tlA = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelineA);
        var tlB = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelineB);

        bool wasActive = a.gameObject.activeSelf;
        var bGo = Object.Instantiate(a.gameObject, a.parent);
        bGo.name = NameB;
        bGo.SetActive(false);
        var b = bGo.transform;
        b.SetSiblingIndex(a.GetSiblingIndex() + 1);
        a.gameObject.SetActive(wasActive);

        var dirB = b.GetComponentInChildren<PlayableDirector>(true);
        dirB.playableAsset = tlB;
        var tracksA = tlA.GetOutputTracks().ToList();
        var tracksB = tlB.GetOutputTracks().ToList();
        if (tracksA.Count != tracksB.Count) { Debug.LogError("[EscapeB] timeline copy has different tracks"); return; }
        for (int i = 0; i < tracksA.Count; i++)
        {
            var objA = dirA.GetGenericBinding(tracksA[i]);
            dirB.ClearGenericBinding(tracksA[i]);
            dirB.SetGenericBinding(tracksB[i], Counterpart(objA, a, b));
        }
        EditorUtility.SetDirty(dirB);

        // ---- the walk, out on the street
        var walk = new GameObject("Walk Offset (street)").transform;
        walk.SetParent(b, false);
        walk.localPosition = WalkB - WalkA;
        foreach (var n in new[] { "PlayerTimelineHolder (4)", "Shot5" }) b.Find(n).SetParent(walk, false);

        // ---- the rest, outside the cloth
        var rest = new GameObject("Rest Offset (outside the cloth)").transform;
        rest.SetParent(b, false);
        rest.localPosition = RestShift;
        foreach (var n in new[] { "KhalidSadPose", "Shot6", "Shot7", "Shot8", "Shot9", "Shot10", "Shot11", "Shot12", "PlayerTimelineHolder (5)", "SpawnPoint",
                                  "Escape Coward Talk" })   // the coward finding him (EscapeCowardTalkBuilder) comes along
        {
            var t = b.Find(n);
            if (t != null) t.SetParent(rest, false);
        }

        // Shot7: down the empty street instead of down the Block 3 corridor (which is behind the cloth now)
        rest.Find("Shot7").SetPositionAndRotation(Shot7Pos, Quaternion.Euler(Shot7Euler));

        // Shot11: its dolly went east down the Block 3 corridor (behind the cloth now) - in B it glides up to the cloth instead.
        // The track offset is in the parent's space (Rest Offset), hence minus RestShift.
        var shot11 = rest.Find("Shot11");
        for (int i = 0; i < tracksB.Count; i++)
        {
            if (!(tracksB[i] is AnimationTrack at) || dirB.GetGenericBinding(at) == null) continue;
            var bound = dirB.GetGenericBinding(at) as Component;
            if (bound == null || bound.transform != shot11 || at.infiniteClip == null) continue;
            at.infiniteClipOffsetPosition = Shot11Start - RestShift;
            at.infiniteClipOffsetEulerAngles = Shot11Euler;
            EditorUtility.SetDirty(at);
        }
        shot11.SetPositionAndRotation(Shot11Start, Quaternion.Euler(Shot11Euler));
        EditorUtility.SetDirty(tlB);

        // ---- the run, down the street (Shot4)
        BuildRun(b, dirB, tracksB);

        // ---- the door picks A or B
        var chooserT = actionSeq.Find(ChooserName);
        if (chooserT == null) { chooserT = new GameObject(ChooserName).transform; chooserT.SetParent(actionSeq, false); }
        var chooser = chooserT.GetComponent<EscapeCutsceneChooser>() ?? chooserT.gameObject.AddComponent<EscapeCutsceneChooser>();
        var cso = new SerializedObject(chooser);
        cso.FindProperty("block3Cloth").objectReferenceValue = block3;
        cso.FindProperty("versionA").objectReferenceValue = a.gameObject;
        cso.FindProperty("versionB").objectReferenceValue = bGo;
        cso.ApplyModifiedPropertiesWithoutUndo();

        var dso = new SerializedObject(door.GetComponent<DoorInteractable>());
        var calls = dso.FindProperty("OnInteractedSuccess.m_PersistentCalls.m_Calls");
        bool rewired = false;
        for (int i = 0; i < calls.arraySize; i++)
        {
            var c = calls.GetArrayElementAtIndex(i);
            var target = c.FindPropertyRelative("m_Target").objectReferenceValue;
            bool isA = target == a.gameObject && c.FindPropertyRelative("m_MethodName").stringValue == "SetActive";
            bool isChooser = target == chooser && c.FindPropertyRelative("m_MethodName").stringValue == "Play";
            if (!isA && !isChooser) continue;
            c.FindPropertyRelative("m_Target").objectReferenceValue = chooser;
            c.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(EscapeCutsceneChooser).AssemblyQualifiedName;
            c.FindPropertyRelative("m_MethodName").stringValue = "Play";
            c.FindPropertyRelative("m_Mode").intValue = (int)PersistentListenerMode.Void;
            rewired = true;
        }
        dso.ApplyModifiedPropertiesWithoutUndo();
        if (!rewired) Debug.LogWarning("[EscapeB] the door's 'ActionSequenceCutscene.SetActive' call was not found - wire EscapeCutsceneChooser.Play yourself");

        // ---- hold-E skip works for B too
        var skipper = all.Select(t => t.GetComponent<CutsceneSkipper>()).FirstOrDefault(s => s != null);
        if (skipper != null)
        {
            var sso = new SerializedObject(skipper);
            var arr = sso.FindProperty("directors");
            bool has = false;
            for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == dirB) has = true;
            for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(i);
            if (!has) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = dirB; }
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[EscapeB] built " + NameB + " (" + TimelineB + "), spawn at " + rest.Find("SpawnPoint").position);
    }

    static void BuildRun(Transform b, PlayableDirector dirB, System.Collections.Generic.List<TrackAsset> tracksB)
    {
        var holder = b.Find("PlayerTimelineHolder (3)");
        var runner = holder.GetChild(0);                                  // MainCharacterAnimated
        var shot4 = b.Find("Shot4");
        double t0 = 0, t1 = 0;
        foreach (var tr in tracksB)
            if (tr is ActivationTrack && dirB.GetGenericBinding(tr) as GameObject == shot4.gameObject)
                foreach (var c in tr.GetClips()) { t0 = c.start; t1 = c.end; }
        if (t1 <= t0) { Debug.LogError("[EscapeB] Shot4's clip not found"); return; }

        foreach (var tr in tracksB)
        {
            var bound = dirB.GetGenericBinding(tr);
            // Khalid on screen and running for the whole shot
            if (tr is ActivationTrack && bound as GameObject == runner.gameObject)
                foreach (var c in tr.GetClips()) c.duration = t1 - c.start;
            if (tr is AnimationTrack at && bound is Component comp && comp.transform == runner)
                foreach (var c in at.GetClips())
                {
                    c.duration = t1 - c.start;
                    if (c.asset is AnimationPlayableAsset ap) ap.loop = AnimationPlayableAsset.LoopMode.On;
                }
            // his path: holder local = offsetRot * curve + offsetPos, Khalid = holder + offsetRot * his local position
            if (tr is AnimationTrack ht && bound is Component hc && hc.transform == holder && ht.infiniteClip != null)
            {
                var rot = Quaternion.Euler(ht.infiniteClipOffsetEulerAngles);
                Vector3 Curve(Vector3 world) => Quaternion.Inverse(rot) * (world - ht.infiniteClipOffsetPosition) - runner.localPosition;
                Vector3 c0 = Curve(RunFrom), c1 = Curve(RunTo);
                var clip = ht.infiniteClip;
                foreach (var axis in new[] { "x", "y", "z" })
                {
                    int i = axis == "x" ? 0 : axis == "y" ? 1 : 2;
                    var curve = AnimationCurve.Linear((float)t0, c0[i], (float)t1, c1[i]);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition." + axis), curve);
                }
                EditorUtility.SetDirty(clip);
            }
            // no gate on the road
            if (tr is AudioTrack au && au.GetClips().Any(c => c.displayName.StartsWith("Gate")))
                au.muted = true;
            EditorUtility.SetDirty(tr);
        }

        // Shot4 follows him: just ahead and to the side, low, looking back at his chest
        var vcam = shot4.GetComponents<MonoBehaviour>().First(m => m.GetType().Name == "CinemachineVirtualCamera");
        var vso = new SerializedObject(vcam);
        vso.FindProperty("m_Follow").objectReferenceValue = runner;
        vso.FindProperty("m_LookAt").objectReferenceValue = runner;
        vso.ApplyModifiedPropertiesWithoutUndo();
        foreach (var m in shot4.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var so = new SerializedObject(m);
            if (m.GetType().Name == "CinemachineTransposer")
            {
                so.FindProperty("m_BindingMode").intValue = 4;                     // world space
                so.FindProperty("m_FollowOffset").vector3Value = Shot4FollowOffset;
                so.FindProperty("m_XDamping").floatValue = 0.6f;
                so.FindProperty("m_YDamping").floatValue = 0.6f;
                so.FindProperty("m_ZDamping").floatValue = 0.6f;
            }
            if (m.GetType().Name == "CinemachineComposer")
                so.FindProperty("m_TrackedObjectOffset").vector3Value = new Vector3(0f, 1.25f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        shot4.position = RunFrom + Shot4FollowOffset;
    }

    /// <summary>A's onCutsceneComplete without the old Al-Sarab redirect (and without dead listeners).</summary>
    static void CleanCompleteEvent(CutsceneTrigger trig)
    {
        var f = typeof(CutsceneTrigger).GetField("onCutsceneComplete", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var ev = (UnityEvent)f.GetValue(trig);
        for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            var t = ev.GetPersistentTarget(i);
            if (t == null) UnityEventTools.RemovePersistentListener(ev, i);
        }
        EditorUtility.SetDirty(trig);
    }

    /// <summary>The object in copy B that sits where 'o' sits in A (same path, same component type); outside objects stay.</summary>
    static Object Counterpart(Object o, Transform a, Transform b)
    {
        if (o == null) return null;
        Transform t = o is Component c ? c.transform : o is GameObject g ? g.transform : null;
        if (t == null || !(t == a || t.IsChildOf(a))) return o;
        var path = new System.Collections.Generic.List<int>();
        for (var x = t; x != a; x = x.parent) path.Insert(0, x.GetSiblingIndex());
        Transform y = b;
        foreach (int i in path) y = y.GetChild(i);
        if (o is GameObject) return y.gameObject;
        return y.GetComponent(o.GetType());
    }
}
