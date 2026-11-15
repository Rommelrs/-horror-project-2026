using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Cutscenes > Build Kidnap Sequence (Game scene): the Runner snatches Adel at the park toilet after the store and
/// Khalid chases it to Block 1 (KidnapSequence). Also: removes the candy wrapper interactable and the old "ghost Adel" /
/// RunnerTrail glimpses the after-store events switched on (the kidnap replaces them), and gives Khalid's locomotion tree a
/// faster run above his normal sprint (Khalid_Run_Mocap_Normal) that only the chase reaches. Safe to run again.
/// </summary>
public static class KidnapSequenceBuilder
{
    const string RootName = "Kidnap Sequence";
    const string Dir = "Assets/Cutscenes/Kidnap";
    const string AdelFbx = "Assets/Models/Adel/Adel_ All animations_ In one track.fbx";
    const string AdelController = Dir + "/Adel_Kidnap.controller";
    const string RunnerPrefab = "Assets/Prefab/Enemy/RunnerPuppet.prefab";
    const string KhalidController = "Assets/MainCharacter/MainCharacterAnim.controller";
    const string FastRunClip = "Assets/MainCharacter/Khalid_Run_Mocap_Normal.anim";
    const string NormalRunClip = "Assets/MainCharacter/Khalid_Run_Mocap_Slow.anim";
    const float FastRunNativeSpeed = 5.9f;     // m/s of the Running.fbx mocap at timeScale 1
    public const float ChaseSprint = 3.8f;     // Khalid's sprint in the chase (normal 3.2)
    // user: "faster the animations but slow the movement speed" -> the mocap plays quicker than his ground speed asks for
    const float ChaseRunTimeScale = 0.95f;
    const string AdelWalkLoop = Dir + "/Adel_Walk_Loop.anim";
    const string WindClip = "Assets/Audio/Cutscenes/CowardMeeting1/Wind_Loop.wav";
    const string KnockdownMusic = "Assets/Audio/Glass Table/Music.mp3";
    const float HoldThreshold = 3.25f;         // the normal run stays pure up to here (normal sprint tops out at 3.2)

    static readonly Vector3 TriggerPos = new Vector3(-229.4f, 2.76f, 78.6f);   // north of the shot's camera (75.7): Khalid stays out of frame

    // the user's drawn line: east along the walkway, across the top of the oval garden, south down the walk east of it,
    // through the gate in the park fence and into Block 1
    static readonly Vector3[] Path =
    {
        new Vector3(-228.6f, 2.85f, 66.0f),     // right past Khalid at the end of the lane
        new Vector3(-220.0f, 2.85f, 67.6f),
        new Vector3(-210.0f, 2.85f, 67.6f),
        new Vector3(-205.5f, 2.85f, 63.5f),
        new Vector3(-201.5f, 2.85f, 59.0f),
        new Vector3(-195.5f, 2.85f, 57.2f),
        new Vector3(-188.0f, 2.85f, 56.0f),
        new Vector3(-182.0f, 2.85f, 53.0f),
        new Vector3(-179.6f, 2.85f, 46.0f),
        new Vector3(-178.4f, 2.85f, 33.0f),
        new Vector3(-177.6f, 2.5f, 22.0f),
        new Vector3(-177.15f, 1.6f, 13.8f),     // in front of the closed street gate of Block 1 ...
        new Vector3(-177.0f, 1.5f, 9.4f),       // ... over it (VaultLanding) ...
        new Vector3(-176.9f, 1.5f, 2.0f),       // ... and away down the block, where the fog takes him
        new Vector3(-176.8f, 1.5f, -8.0f),
    };
    const int VaultLanding = 12;                // index in Path of the point he lands on behind the gate
    const string RunnerSnarl = "Assets/Audio/Enemies/Runner/Ruuner_Attack 1.mp3";

    const string AdelKey = "Hey!! Help me!!", AdelAr = "هيييه!! ساعدوني!!";
    const string KhalidKey = "Adel!!", KhalidAr = "عادل!!";

    [MenuItem("Tools/Cutscenes/Build Kidnap Sequence (Game scene)")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!scene.isLoaded) { Debug.LogError("[Kidnap] open the Game scene first"); return; }

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        Transform park = all.FirstOrDefault(t => t.name == "Park Gameplay");
        if (park == null) { Debug.LogError("[Kidnap] Park Gameplay not found"); return; }

        // ---- the old guidance this replaces
        RemoveOldGuidance(all);
        all = all.Where(t => t != null).ToList();

        // ---- actors + controller
        var adelCtrl = BuildAdelController();
        var old = park.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(RootName);
        root.transform.SetParent(park, true);
        root.transform.position = TriggerPos;
        var box = root.AddComponent<BoxCollider>();
        box.isTrigger = true; box.size = new Vector3(3.6f, 3f, 1.4f); box.center = new Vector3(0f, 1.2f, 0f);

        var runnerGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefab), scene);
        runnerGo.name = "Kidnap Runner";
        runnerGo.transform.SetParent(root.transform, true);
        runnerGo.transform.position = new Vector3(-251.5f, 2.9f, 66.6f);
        runnerGo.SetActive(false);

        var adelGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AdelFbx), scene);
        adelGo.name = "Kidnap Adel";
        adelGo.transform.SetParent(root.transform, true);
        adelGo.transform.position = new Vector3(-238.25f, 2.9f, 67.25f);
        var adelAnim = adelGo.GetComponent<Animator>();
        if (adelAnim == null) adelAnim = adelGo.AddComponent<Animator>();
        adelAnim.runtimeAnimatorController = adelCtrl;
        adelAnim.applyRootMotion = false;
        adelAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        adelGo.SetActive(false);

        var seq = root.AddComponent<KidnapSequence>();
        var so = new SerializedObject(seq);
        so.FindProperty("runner").objectReferenceValue = runnerGo.GetComponent<RunnerPuppet>();
        so.FindProperty("adel").objectReferenceValue = adelAnim;
        so.FindProperty("chaseSprintSpeed").floatValue = ChaseSprint;
        var pathProp = so.FindProperty("path");
        pathProp.arraySize = Path.Length;
        for (int i = 0; i < Path.Length; i++) pathProp.GetArrayElementAtIndex(i).vector3Value = Path[i];
        all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var reveal = all.Select(t => t.GetComponent<MapLocationReveal>()).FirstOrDefault(r => r != null && r.name == "MapLocationReveal (1)");
        var hint = all.Select(t => t.GetComponent<MapHintPrompt>()).FirstOrDefault(h => h != null && h.transform.parent == park);
        // no map reveal: MapLocationReveal (1) (the candy wrapper's) marks the BARBER, not Block 1 (user: remove it)
        so.FindProperty("reveal").objectReferenceValue = null;
        so.FindProperty("mapHint").objectReferenceValue = hint;
        so.FindProperty("sfxGroup").objectReferenceValue = SfxGroup();
        so.FindProperty("musicGroup").objectReferenceValue = Group("Music");
        so.FindProperty("music").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(KnockdownMusic);
        so.FindProperty("windLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(WindClip);
        so.FindProperty("doorOpen").objectReferenceValue = FindClip(n => n.Contains("door") && (n.Contains("open") || n.Contains("creak")));
        so.FindProperty("runnerScreech").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(RunnerSnarl);
        so.FindProperty("vaultSegment").intValue = VaultLanding;
        so.FindProperty("chaseEnd").vector3Value = new Vector3(-177f, 1.6f, 14.5f);
        so.FindProperty("chaseEndRadius").floatValue = 5f;
        so.FindProperty("adelLine").stringValue = AdelKey;
        so.FindProperty("khalidLine").stringValue = KhalidKey;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[Kidnap] reveal " + (reveal ? reveal.name : "NONE") + ", hint " + (hint ? hint.name : "NONE") + ", door clip " +
                  (so.FindProperty("doorOpen").objectReferenceValue ? so.FindProperty("doorOpen").objectReferenceValue.name : "none") + ", runner clip " +
                  (so.FindProperty("runnerScreech").objectReferenceValue ? so.FindProperty("runnerScreech").objectReferenceValue.name : "none"));

        Line(AdelKey, AdelKey, AdelAr);
        Line(KhalidKey, KhalidKey, KhalidAr);
        Line("I better chase him!", "I better chase him!", "لازم ألحقه!");
        AssetDatabase.SaveAssets();

        AddChaseRun();
        CheckPath();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Kidnap] built - save the Game scene");
    }

    // ------------------------------------------------------------------ old guidance

    static void RemoveOldGuidance(List<Transform> all)
    {
        var cutTrigger = all.FirstOrDefault(t => t.name == "Trigger" && t.parent != null && t.parent.name == "Cutscenes");
        var afterStore = all.FirstOrDefault(t => t.name == "Cutscene_AfterStore");
        string[] killOn = { "CandyWrapperInteractable", "RunnerTrail", "AdelRunning (2)", "AdelRunning (3)" };
        if (cutTrigger != null) RemoveCalls(cutTrigger.GetComponent<OnPlayerTrigger>(), "onTrigger", killOn);
        if (afterStore != null) RemoveCalls(afterStore.GetComponent<CutsceneTrigger>(), "onCutsceneComplete", killOn);

        foreach (var n in new[] { "CandyWrapperInteractable", "CandyWrapperWalkToPosition" })
        {
            var t = all.FirstOrDefault(x => x != null && x.name == n);
            if (t != null) { Debug.Log("[Kidnap] removed " + n); Object.DestroyImmediate(t.gameObject); }
        }
        // the ghost Adels / trail runner stay switched off (not deleted: SaveableTriggers, the user may want them back)
        foreach (var n in new[] { "RunnerTrail" })
        {
            var t = all.FirstOrDefault(x => x != null && x.name == n);
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    /// <summary>Drops every "X.SetActive(true)" call on the named objects from a UnityEvent (switching them off stays).</summary>
    static void RemoveCalls(Component c, string eventName, string[] targets)
    {
        if (c == null) return;
        var so = new SerializedObject(c);
        var arr = so.FindProperty(eventName + ".m_PersistentCalls.m_Calls");
        for (int i = arr.arraySize - 1; i >= 0; i--)
        {
            var e = arr.GetArrayElementAtIndex(i);
            var target = e.FindPropertyRelative("m_Target").objectReferenceValue;
            string method = e.FindPropertyRelative("m_MethodName").stringValue;
            bool on = e.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue;
            string name = target is GameObject g ? g.name : target is Component cc ? cc.name : null;
            if (name != null && targets.Contains(name) && method == "SetActive" && on)
            {
                Debug.Log("[Kidnap] " + c.name + "." + eventName + ": removed " + name + ".SetActive(true)");
                arr.DeleteArrayElementAtIndex(i);
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Adel

    static RuntimeAnimatorController BuildAdelController()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Cutscenes", "Kidnap");
        var clips = AssetDatabase.LoadAllAssetsAtPath(AdelFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToList();
        AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AdelController);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(AdelController);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
        var idle = sm.AddState("Idle"); idle.motion = Clip("Adel_Idle");
        // the FBX's walk does not loop: a looping copy
        var srcWalk = Clip("Adel_ Walking normally");
        var walkLoop = AssetDatabase.LoadAssetAtPath<AnimationClip>(AdelWalkLoop);
        if (walkLoop == null && srcWalk != null)
        {
            walkLoop = Object.Instantiate(srcWalk);
            walkLoop.name = "Adel_Walk_Loop";
            AssetDatabase.CreateAsset(walkLoop, AdelWalkLoop);
        }
        if (walkLoop != null)
        {
            var st = AnimationUtility.GetAnimationClipSettings(walkLoop);
            st.loopTime = true; st.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(walkLoop, st);
            EditorUtility.SetDirty(walkLoop);
        }
        var walk = sm.AddState("Walk"); walk.motion = walkLoop != null ? walkLoop : srcWalk;
        var run = sm.AddState("Run"); run.motion = Clip("Adel_Running");
        sm.defaultState = idle;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // ------------------------------------------------------------------ Khalid's faster run

    /// <summary>Locomotion (1D on Velocity) = walk tree, run tree [+ run copy at 3.25 + chase tree at ChaseSprint]. The copy keeps
    /// the normal sprint (3.2) on the pure normal run; above it the chase blends into the fast mocap run at its own pace.</summary>
    static void AddChaseRun()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(KhalidController);
        var fast = AssetDatabase.LoadAssetAtPath<AnimationClip>(FastRunClip);
        var slow = AssetDatabase.LoadAssetAtPath<AnimationClip>(NormalRunClip);
        if (ctrl == null || fast == null || slow == null) { Debug.LogError("[Kidnap] controller / run clips missing"); return; }
        BlendTree loco = null;
        foreach (var l in ctrl.layers)
            foreach (var s in l.stateMachine.states)
                if (s.state.name == "WalkMove" && s.state.motion is BlendTree bt && bt.name == "Locomotion") loco = bt;
        if (loco == null) { Debug.LogError("[Kidnap] Locomotion tree not found"); return; }

        var kids = loco.children.ToList();
        Debug.Log("[Kidnap] Locomotion before: " + string.Join(", ", kids.Select(k => (k.motion ? k.motion.name : "null") + "@" + k.threshold)));
        // drop an earlier build's extra children
        kids = kids.Where(k => k.motion == null || (k.motion.name != "Run (hold)" && k.motion.name != "Chase Run")).ToList();
        int runIdx = kids.FindIndex(k => k.motion is BlendTree b && b.children.Any(c => c.motion == slow));
        if (runIdx < 0) { Debug.LogError("[Kidnap] run tree not found in Locomotion"); return; }
        var runTree = (BlendTree)kids[runIdx].motion;

        BlendTree Copy(string name, bool chase)
        {
            var bt = new BlendTree
            {
                name = name, blendType = runTree.blendType, blendParameter = runTree.blendParameter, blendParameterY = runTree.blendParameterY,
                useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(bt, ctrl);
            var cs = runTree.children.Select(c =>
            {
                if (chase && c.motion == slow) { c.motion = fast; c.timeScale = ChaseRunTimeScale; }
                return c;
            }).ToArray();
            bt.children = cs;
            return bt;
        }
        // remove stale sub-assets from an earlier build
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(KhalidController))
            if (o is BlendTree b && (b.name == "Run (hold)" || b.name == "Chase Run")) Object.DestroyImmediate(b, true);

        var hold = Copy("Run (hold)", false);
        var chaseTree = Copy("Chase Run", true);
        kids.Add(new ChildMotion { motion = hold, threshold = HoldThreshold, timeScale = 1f });
        kids.Add(new ChildMotion { motion = chaseTree, threshold = ChaseSprint, timeScale = 1f });
        loco.useAutomaticThresholds = false;
        loco.children = kids.OrderBy(k => k.threshold).ToArray();
        EditorUtility.SetDirty(loco); EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[Kidnap] Locomotion now: " + string.Join(", ", loco.children.Select(k => (k.motion ? k.motion.name : "null") + "@" + k.threshold)) +
                  " (fast run timeScale " + ChaseRunTimeScale + ")");
    }

    // ------------------------------------------------------------------ checks / helpers

    /// <summary>Logs anything solid Khalid would run into along the line (capsule at body height).</summary>
    static void CheckPath()
    {
        var pts = new List<Vector3> { new Vector3(-234.4f, 2.9f, 65.9f) };
        pts.AddRange(Path);
        int blocked = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 a = Ground(pts[i - 1]), b = Ground(pts[i]);
            Vector3 d = b - a; float len = d.magnitude;
            foreach (var h in Physics.CapsuleCastAll(a + Vector3.up * 0.6f, a + Vector3.up * 1.5f, 0.35f, d.normalized, len, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.distance <= 0f) continue;
                blocked++;
                Debug.LogWarning("[Kidnap] path segment " + i + " (" + a.ToString("F1") + " -> " + b.ToString("F1") + ") hits " + h.collider.name + " at " + h.point.ToString("F1"));
            }
        }
        Debug.Log("[Kidnap] path check: " + blocked + " hits");
    }

    static Vector3 Ground(Vector3 p)
    {
        if (Physics.Raycast(new Vector3(p.x, p.y + 2.5f, p.z), Vector3.down, out var h, 8f, ~0, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
        return p;
    }

    static AudioMixerGroup SfxGroup() => Group("SFX");

    static AudioMixerGroup Group(string name)
    {
        foreach (var g in AssetDatabase.FindAssets("t:AudioMixer"))
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetDatabase.GUIDToAssetPath(g));
            var groups = mixer != null ? mixer.FindMatchingGroups(name) : null;
            if (groups != null) foreach (var gr in groups) if (gr.name == name) return gr;
        }
        return null;
    }

    static AudioClip FindClip(System.Func<string, bool> match)
    {
        foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio", "Assets/Sounds", "Assets/SFX" }.Where(AssetDatabase.IsValidFolder).ToArray()))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (match(System.IO.Path.GetFileNameWithoutExtension(path).ToLower())) return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }

    static void Line(string key, string en, string ar)
    {
        var col = LocalizationEditorSettings.GetStringTableCollection("Subtitles");
        var entry = col.SharedData.GetEntry(key) ?? col.SharedData.AddKey(key);
        foreach (var t in col.StringTables)
        {
            string text = t.LocaleIdentifier.Code.StartsWith("ar") ? ArabicSupport.ArabicFixer.Fix(ar, false, false) : en;
            var e = t.GetEntry(entry.Id);
            if (e == null) t.AddEntry(entry.Id, text); else e.Value = text;
            EditorUtility.SetDirty(t);
        }
        EditorUtility.SetDirty(col.SharedData);
    }
}
