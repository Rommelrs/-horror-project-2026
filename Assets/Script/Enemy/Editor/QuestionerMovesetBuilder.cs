using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Tools > Questioner > Build Spec Moveset: wires the Questioner spec moveset into the project.
///  - TheQuestioner_Moves.fbx (AnimationTools/questioner/q20_moves.py) import settings: generic, loops on the loop clips.
///  - TheQuestionerAnimCon: MoveX / MoveZ params, Q_Locomotion 2D blend (idle / walk / sidewalk), the move states played by
///    name from QuestionerCombatState, AnyState -> Q_DeathKneel (Dead + DeadIndex 2).
///  - QuestionerEnemyNoRunning.prefab (QuestionerEnemy.prefab is its variant): HeadshotEnemy -> QuestionerEnemy (same fields,
///    script swapped in place), move audio cues, the scream's sound-wave material.
///  - Player.prefab: PlayerCloseCombat (grab camera, scream screen echo + ringing, knockback) with its shader / sounds.
/// Re-runnable: replaces what it made before.
/// </summary>
public static class QuestionerMovesetBuilder
{
    const string ModelPath = "Assets/Models/TheQuestioner/TheQuestioner.fbx";
    const string MovesPath = "Assets/Models/TheQuestioner/TheQuestioner_Moves.fbx";
    const string ControllerPath = "Assets/Animations/EnemyAnimCon/TheQuestionerAnimCon.controller";
    const string PrefabPath = "Assets/Prefab/Enemy/QuestionerEnemyNoRunning.prefab";
    const string PlayerPrefabPath = "Assets/Prefab/Player/Player.prefab";
    const string MovesAudio = "Assets/Audio/Enemies/Questioner/Moves/";
    const string WaveShaderPath = "Assets/Shaders/QuestionerScreamWave.shader";
    const string WaveMaterialPath = "Assets/Models/TheQuestioner/QuestionerScreamWave.mat";
    const string EchoShaderPath = "Assets/Shaders/QuestionerScreamEcho.shader";

    static readonly string[] LoopClips = { "Q_GrabRush", "Q_GrabHold", "Q_StaggerLoop" };

    // state -> (clip, speed)
    static readonly (string state, string clip, float speed)[] MoveStates =
    {
        ("Q_ScreamWindup", "Q_ScreamWindup", 1f), ("Q_Scream", "Q_Scream", 1f), ("Q_ScreamRecover", "Q_ScreamRecover", 1f),
        ("Q_GrabWindup", "Q_GrabWindup", 1f), ("Q_GrabRush", "Q_GrabRush", 1.9f), ("Q_GrabHold", "Q_GrabHold", 1f),
        ("Q_Bite", "Q_Bite", 1f), ("Q_PushedOff", "Q_PushedOff", 1f), ("Q_Swipe", "Q_Swipe", 1f),
        ("Q_StaggerDown", "Q_StaggerDown", 1f), ("Q_StaggerLoop", "Q_StaggerLoop", 1f), ("Q_StaggerUp", "Q_StaggerUp", 1f),
        ("Q_DeathKneel", "Q_DeathKneel", 1f), ("Q_Hurt", "Q_Hurt", 1f),
        ("Q_GrabScream", "Q_GrabScream", 1f), ("Q_BiteSlow", "Q_BiteSlow", 1f),
    };

    [MenuItem("Tools/Questioner/Build Spec Moveset")]
    public static void Build()
    {
        ImportMoves();
        BuildController();
        BuildPrefab();
        BuildPlayer();
        BuildKhalidLayer();
        AssetDatabase.SaveAssets();
        Debug.Log("[QuestionerMovesetBuilder] Questioner spec moveset built");
    }

    // ---------------- clips ----------------

    static void ImportMoves()
    {
        var src = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        var mi = AssetImporter.GetAtPath(MovesPath) as ModelImporter;
        if (mi == null) { Debug.LogError("[QuestionerMovesetBuilder] " + MovesPath + " missing - run AnimationTools/questioner/q20_moves.py"); return; }
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.globalScale = src.globalScale;
        mi.useFileScale = src.useFileScale;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importAnimation = true;
        mi.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = LoopClips.Contains(c.name);
            c.keepOriginalPositionY = true;
        }
        mi.clipAnimations = clips;
        mi.SaveAndReimport();

        // his idle holds its last frame otherwise (he freezes when he stands still in the fight)
        var main = src.clipAnimations;
        bool changed = false;
        foreach (var c in main) if (c.name == "Questioner_Idle" && !c.loopTime) { c.loopTime = true; changed = true; }
        if (changed) { src.clipAnimations = main; src.SaveAndReimport(); }
    }

    static AnimationClip FindClip(string name)
    {
        foreach (var path in new[] { MovesPath, ModelPath })
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && c.name == name) return c;
        Debug.LogWarning("[QuestionerMovesetBuilder] clip not found: " + name);
        return null;
    }

    // ---------------- animator ----------------

    static void BuildController()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        foreach (var p in new[] { "MoveX", "MoveZ" })
            if (!ctrl.parameters.Any(x => x.name == p)) ctrl.AddParameter(p, AnimatorControllerParameterType.Float);

        var sm = ctrl.layers[0].stateMachine;
        // remove what an earlier run made
        foreach (var st in sm.states.ToArray())
            if (st.state.name.StartsWith("Q_")) sm.RemoveState(st.state);
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            if (o is BlendTree bt && bt.name == "Q_LocomotionTree") Object.DestroyImmediate(bt, true);

        // locomotion while fighting: local velocity in m/s (walk 1.0 forward, strafe 1.2 sideways)
        var tree = new BlendTree
        {
            name = "Q_LocomotionTree", blendType = BlendTreeType.FreeformCartesian2D, blendParameter = "MoveX", blendParameterY = "MoveZ",
            useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        tree.AddChild(FindClip("Questioner_Idle"), new Vector2(0f, 0f));
        tree.AddChild(FindClip("Questioner_Walk2"), new Vector2(0f, 1.0f));
        tree.AddChild(FindClip("Questioner_Walk1"), new Vector2(0f, 1.9f));
        // (the clip named "left" carries his body to his right - see the old sidestep code)
        tree.AddChild(FindClip("Questioner_SidewalkLeft"), new Vector2(1.2f, 0f));
        tree.AddChild(FindClip("Questioner_SidewalkRight"), new Vector2(-1.2f, 0f));
        tree.AddChild(FindClip("Questioner_Walk2"), new Vector2(0f, -0.9f));
        var kids = tree.children;
        kids[3].timeScale = kids[4].timeScale = 1.5f;      // sidewalk stride is short: quicker steps, less skating
        kids[5].timeScale = -1f;                           // walking backwards
        tree.children = kids;

        var loco = sm.AddState("Q_Locomotion", new Vector3(650, -120, 0));
        loco.motion = tree;
        int i = 0;
        foreach (var (state, clip, speed) in MoveStates)
        {
            var st = sm.AddState(state, new Vector3(900 + (i / 7) * 250, -200 + (i % 7) * 60, 0));
            st.motion = FindClip(clip);
            st.speed = speed;
            st.writeDefaultValues = true;
            i++;
        }

        // death from the stagger: falls forward from his knees
        foreach (var t in sm.anyStateTransitions.ToArray())
            if (t.destinationState != null && t.destinationState.name == "Q_DeathKneel") sm.RemoveAnyStateTransition(t);
        var kneel = sm.states.First(s => s.state.name == "Q_DeathKneel").state;
        var tr = sm.AddAnyStateTransition(kneel);
        tr.AddCondition(AnimatorConditionMode.If, 0, "Dead");
        tr.AddCondition(AnimatorConditionMode.Equals, 2, "DeadIndex");
        tr.duration = 0.15f;
        tr.canTransitionToSelf = false;
        EditorUtility.SetDirty(ctrl);
    }

    // ---------------- prefab ----------------

    static AudioClip[] Clips(params string[] paths)
    {
        var list = new List<AudioClip>();
        foreach (var p in paths)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (c != null) list.Add(c); else Debug.LogWarning("[QuestionerMovesetBuilder] audio missing: " + p);
        }
        return list.ToArray();
    }

    static void BuildPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        MonoScript script = null;
        foreach (var g in AssetDatabase.FindAssets("QuestionerEnemy t:MonoScript"))
        {
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
            if (ms != null && ms.GetClass() == typeof(QuestionerEnemy)) script = ms;
        }
        var old = root.GetComponent<HeadshotEnemy>();
        if (old != null && !(old is QuestionerEnemy))
        {
            var so = new SerializedObject(old);
            so.FindProperty("m_Script").objectReferenceValue = script;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var q = root.GetComponent<QuestionerEnemy>();
        if (q == null) { Debug.LogError("[QuestionerMovesetBuilder] script swap failed"); PrefabUtility.UnloadPrefabContents(root); return; }
        q.specMoveset = true;
        q.inhaleSFX = Clips(MovesAudio + "Q_Inhale.wav");
        // the recorded "وينه؟" line (loudest mix of the store-room takes)
        q.screamSFX = Clips("Assets/Audio/StoreRoomSFX/Weenah, close up of the monster.mp3");
        q.tuning.staggerTime = 4f;
        q.tuning.biteHoldTime = 1.45f;          // the slow bite (was 1.0 for the old instant one)
        q.tuning.screamEffectTime = 6f;         // the blur / echo, sway and ringing (was 1.0)
        q.grabCueSFX = Clips(MovesAudio + "Q_GrabCue.wav");
        q.hissSFX = Clips(MovesAudio + "Q_Hiss.wav");
        q.whisperSFX = Clips(MovesAudio + "Q_Whisper.wav");
        q.biteSFX = Clips(MovesAudio + "Q_Bite.wav");
        q.swipeSFX = Clips(MovesAudio + "Q_Claw_1.wav", MovesAudio + "Q_Claw_2.wav");
        q.staggerSFX = Clips(MovesAudio + "Q_Muffled_1.wav", MovesAudio + "Q_Muffled_2.wav", MovesAudio + "Q_Muffled_5.wav");
        q.screamWaveMaterial = WaveMaterial();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static Material WaveMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WaveMaterialPath);
        if (mat != null) return mat;
        var sh = AssetDatabase.LoadAssetAtPath<Shader>(WaveShaderPath);
        if (sh == null) { Debug.LogError("[QuestionerMovesetBuilder] missing " + WaveShaderPath); return null; }
        mat = new Material(sh) { name = "QuestionerScreamWave" };
        AssetDatabase.CreateAsset(mat, WaveMaterialPath);
        return mat;
    }

    // ---------------- Khalid's reactions ----------------

    const string KhalidController = "Assets/MainCharacter/MainCharacterAnim.controller";

    /// <summary>Full-body override layer on top of everything, weighted by PlayerCloseCombat: Grabbed (loop) / Bitten /
    /// BittenHold (held, ends bitten) / PushOff (shoves him off from the bitten pose)
    /// (AnimationTools/questioner/khalid_cc_build.py -> KhalidClipImporter).</summary>
    static void BuildKhalidLayer()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(KhalidController);
        var layers = ctrl.layers.ToList();
        for (int i = layers.Count - 1; i >= 0; i--)
            if (layers[i].name == "CloseCombat")
            {
                if (layers[i].stateMachine != null) Object.DestroyImmediate(layers[i].stateMachine, true);
                layers.RemoveAt(i);
            }
        var sm = new AnimatorStateMachine { name = "CloseCombat", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, ctrl);
        var empty = sm.AddState("Empty", new Vector3(300, 0, 0));
        sm.defaultState = empty;
        int i2 = 0;
        foreach (var (state, clip) in new[] { ("Grabbed", "Khalid_CC_Grabbed"), ("Bitten", "Khalid_CC_Bitten"), ("BittenHold", "Khalid_CC_BittenHold"), ("PushOff", "Khalid_CC_PushOff") })
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/MainCharacter/" + clip + ".anim");
            if (c == null) { Debug.LogWarning("[QuestionerMovesetBuilder] Khalid clip missing: " + clip + " (run khalid_cc_build.py + KhalidClipImporter)"); continue; }
            var st = sm.AddState(state, new Vector3(300, 80 + 60 * i2++, 0));
            st.motion = c;
        }
        layers.Add(new AnimatorControllerLayer { name = "CloseCombat", stateMachine = sm, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override });
        ctrl.layers = layers.ToArray();
        EditorUtility.SetDirty(ctrl);
    }

    // ---------------- player ----------------

    static void BuildPlayer()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        var cc = root.GetComponent<PlayerCloseCombat>();
        if (cc == null) cc = root.AddComponent<PlayerCloseCombat>();
        cc.screamEchoShader = AssetDatabase.LoadAssetAtPath<Shader>(EchoShaderPath);
        if (cc.screamEchoShader == null) Debug.LogError("[QuestionerMovesetBuilder] missing " + EchoShaderPath);
        var ring = Clips(MovesAudio + "Q_ScreamRing.wav");
        cc.ringingSFX = ring.Length > 0 ? ring[0] : null;
        cc.bittenSFX = new AudioClip[0];          // Health's own hurt sound plays on the damage
        const string effort = "Assets/Audio/NPC or Player/Effort Sounds/";
        cc.pushOffSFX = Clips(effort + "01. Effort Grunt (Male).wav", effort + "03. Effort Grunt (Male).wav");
        // the grab camera: beside his left shoulder, a three-quarter view of the Questioner's face at his neck
        var so = new SerializedObject(cc);
        so.FindProperty("grabCamBack").floatValue = 0.28f;
        so.FindProperty("grabCamSide").floatValue = -0.8f;
        so.FindProperty("grabCamUp").floatValue = 0.14f;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
