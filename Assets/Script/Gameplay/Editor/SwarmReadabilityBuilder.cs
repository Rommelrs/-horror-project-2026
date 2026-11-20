using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Block 2 Questioner swarm readability (user 2026-11-20, "do all of these" after the readability analysis). Re-runnable:
/// Tools > Prototype > Build Swarm Readability (Game scene, edit mode).
///  1 the tape cutscene (turn back, the glass, he shakes, "I have to get outta here!")
///  2 Adel calls 3D from the exit          5 the line-up gets an aisle down the middle of the road
///  3 the collapse: the USER's blocks swap as they set them up (BlocksToDestroy off, their rubble on, "I can't go there"
///    at the alley until then); the camera glances at the alley entrance
///  4 the west door locks with a sound + a line
///  6 the escape zone goes off with or without Questioners inside   7 the spawner stops at the collapse
///  8 Arabic for "I got a duct tape"
/// </summary>
public static class SwarmReadabilityBuilder
{
    const string WallName = "Lane Wall (cinder blocks)";
    const string GuideName = "Swarm Escape Guide";
    const string GlanceName = "Glance At (alley entrance)";

    [MenuItem("Tools/Prototype/Build Swarm Readability")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[Swarm] edit mode only"); return; }
        var root = Find("Action Sequence", "Alley 3 Gameplay");
        if (root == null) { Debug.LogError("[Swarm] 'Alley 3 Gameplay/Action Sequence' not found (open the Game scene)"); return; }

        var spawnerGo = Child(root, "ItemPickupSpawner");
        var spawner = spawnerGo.GetComponent<ItemPickupEnemySpawner>();
        var zone1 = Child(root, "WeakpointHitArea (1)").GetComponent<DangerZone>();
        var lockTrigger = Child(root, "LockdoorTrigger").GetComponent<DoorLockTrigger>();
        var exitDoor = Child(root, "DoorInteractableBlocks").transform;
        var blocks = Child(root, "Blocks");
        var stopTrigger = Child(root, "SpawnerStopTrigger");
        var blocksToDestroy = Child(root, "BlocksToDestroy");
        var retreatLane = Child(root, "AutoRetreatTrigger (1)");
        var spawnPos3 = Child(spawnerGo.transform, "SpawnPos 3");
        var spawnPos4 = Child(spawnerGo.transform, "SpawnPos 4");
        var lineUp = Child(root, "ChorusLineUp").transform;
        var kidVoice = zone1.GetComponentInChildren<AudioSource>(true);

        // ---------------------------------------------------------------- lines
        var goal = Line("SWARM_GOAL", "I have to get outta here!", "لازم أطلع من هني!");
        var locked = Line("SWARM_LOCKED", "It's locked... I'm trapped.", "انقفل... انحبست.");
        var call = Line("SWARM_ADEL_CALL", "Khalid! Over here!", "خالد! هني!");
        GameUiArabic("I got a duct tape", "لقيت شريط لاصق.");

        // ---------------------------------------------------------------- 3: the user's blocks, as they set them up
        // (user 2026-11-20: my toppling physics ruined their collapse - the zone swaps them again, untouched by the guide)
        var oldWall = root.Find(WallName);
        if (oldWall != null) Object.DestroyImmediate(oldWall.gameObject);
        int removedBodies = 0;
        foreach (var rb in blocksToDestroy.GetComponentsInChildren<Rigidbody>(true))
        {
            if (!PrefabUtility.IsAddedComponentOverride(rb)) continue;     // only the bodies my first build added
            Object.DestroyImmediate(rb);
            removedBodies++;
        }

        // ---------------------------------------------------------------- 1: Khalid's shaking (the tape cutscene)
        EnsureCutsceneStates();

        // ---------------------------------------------------------------- the guide
        var guideT = root.Find(GuideName);
        var guideGo = guideT != null ? guideT.gameObject : new GameObject(GuideName);
        guideGo.transform.SetParent(root, false);
        guideGo.transform.position = exitDoor.position;
        if (!guideGo.TryGetComponent(out SwarmEscapeGuide guide)) guide = guideGo.AddComponent<SwarmEscapeGuide>();
        var g = new SerializedObject(guide);
        g.FindProperty("sfxGroup").objectReferenceValue = kidVoice != null ? kidVoice.outputAudioMixerGroup : null;
        SetLine(g.FindProperty("goalLine"), goal);
        g.FindProperty("lockedDoor").objectReferenceValue = lockTrigger != null ? DoorOf(lockTrigger) : null;
        g.FindProperty("lockSound").objectReferenceValue = Load<AudioClip>("Assets/Audio/Store/OutsideSteps/DoorLock_Loud.wav");
        SetLine(g.FindProperty("lockedLine"), locked);
        g.FindProperty("questionersAt").objectReferenceValue = lineUp;
        g.FindProperty("startledState").stringValue = StartledState;
        g.FindProperty("backAwayState").stringValue = BackAwayState;
        // (saved in the scene by the first version of the cutscene: the new staging needs these)
        g.FindProperty("walkBackSpeed").floatValue = 1.1f;
        g.FindProperty("walkBackMax").floatValue = 2.6f;
        g.FindProperty("beforeTurn").floatValue = 0.7f;
        g.FindProperty("shoulderFov").floatValue = 26f;
        g.FindProperty("backAwaySpeed").floatValue = 1f;
        g.FindProperty("stareFrom").floatValue = 0.8f;
        g.FindProperty("collapseSound").objectReferenceValue = Load<AudioClip>("Assets/Audio/Action Sequence/Block Sounds.wav");
        g.FindProperty("collapseAt").objectReferenceValue = exitDoor;
        SetArray(g.FindProperty("activateWhenOpen"), blocks, stopTrigger);
        SetArray(g.FindProperty("deactivateWhenOpen"), retreatLane, spawnPos4, blocksToDestroy);
        // what the glance looks at: the mouth of the alley off the road (the one "I can't go there" guards until the collapse).
        // Made once - move it freely, a rebuild keeps it where it is.
        var glanceT = guideGo.transform.Find(GlanceName);
        if (glanceT == null)
        {
            glanceT = new GameObject(GlanceName).transform;
            glanceT.SetParent(guideGo.transform, false);
            glanceT.position = new Vector3(-48.2f, 1.4f, 46.5f);   // the mouth itself: in sight from the whole zone
        }
        g.FindProperty("glanceAt").objectReferenceValue = glanceT;
        g.FindProperty("exitPoint").objectReferenceValue = exitDoor;
        g.FindProperty("callClip").objectReferenceValue = Load<AudioClip>("Assets/Audio/Kid voice/Kid voice 12.mp3");
        SetLine(g.FindProperty("callLine"), call);
        g.ApplyModifiedPropertiesWithoutUndo();

        // ---------------------------------------------------------------- 6, 7, 2: the escape zone
        var z = new SerializedObject(zone1);
        z.FindProperty("requireEnemiesInZone").boolValue = false;
        SetArray(z.FindProperty("spawnersToStop"), spawner);
        z.FindProperty("despawnEnemiesFromSpawners").boolValue = false;
        z.FindProperty("stopSpawnerMusic").boolValue = false;
        z.FindProperty("postHitSound").objectReferenceValue = null;      // Adel calls from the exit now (3D), not in Khalid's head
        z.FindProperty("activateSound").objectReferenceValue = null;     // the collapse sound plays at the wall now
        AddCall(z.FindProperty("onTriggered"), guide, "Collapse");
        // the zone switches the user's blocks off again (as they had it)
        var off = z.FindProperty("objectsToDeactivate");
        bool hasBlocks = false;
        for (int i = 0; i < off.arraySize; i++) if (off.GetArrayElementAtIndex(i).objectReferenceValue == blocksToDestroy) hasBlocks = true;
        if (!hasBlocks)
        {
            int at = Mathf.Min(1, off.arraySize);
            off.InsertArrayElementAtIndex(at);
            off.GetArrayElementAtIndex(at).objectReferenceValue = blocksToDestroy;
        }
        z.ApplyModifiedPropertiesWithoutUndo();

        // ---------------------------------------------------------------- 4: the west door
        var l = new SerializedObject(lockTrigger);
        AddCall(l.FindProperty("onLocked"), guide, "OnDoorLocked");
        l.ApplyModifiedPropertiesWithoutUndo();

        // ---------------------------------------------------------------- 1, 5, spawn point in the lane
        var s = new SerializedObject(spawner);
        RemoveCalls(s.FindProperty("onItemPickedUp"), guide);                 // the old instant goal line
        AddCall(s.FindProperty("onItemPickedUp"), guide, "SwarmStarts");      // the glass: the cutscene's close-up
        AddCall(s.FindProperty("onItemDetected"), guide, "BeginIntro");       // he has the tape: the cutscene
        AddSetActive(s.FindProperty("onItemPickedUp"), retreatLane, true);   // the user's "I can't go there" at the alley, back
        s.FindProperty("lineUpWidth").floatValue = 7f;
        s.FindProperty("lineUpAisle").floatValue = 2.6f;
        // SpawnPos 4 (inside the lane) as it was
        var pts = s.FindProperty("spawnPoints");
        for (int i = 0; i < pts.arraySize; i++)
        {
            var p = pts.GetArrayElementAtIndex(i);
            var at = p.FindPropertyRelative("spawnPosition").objectReferenceValue as Transform;
            if (at != null && at.gameObject == spawnPos3) p.FindPropertyRelative("spawnCount").intValue = 5;
        }
        s.ApplyModifiedPropertiesWithoutUndo();
        spawnPos4.SetActive(true);
        retreatLane.SetActive(false);
        // the line-up in the middle of the road (z 47.1 - 54.7 there)
        lineUp.position = new Vector3(lineUp.position.x, lineUp.position.y, 50.9f);

        foreach (var o in new Object[] { guide, zone1, lockTrigger, spawner, lineUp }) EditorUtility.SetDirty(o);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Swarm] readability built: " + removedBodies + " block bodies removed (the user's swap again), guide + cutscene wired, zone/spawner/lock updated, scene saved");
    }

    // ------------------------------------------------------------------ helpers

    // Khalid's cutscene clips: Mixamo mocap retargeted to his rig (AnimationTools/mocap_retarget.py, in Blender;
    // KhalidClipImporter.ImportAll brings them in). Each is its own state on the Movement layer, no transitions:
    // SwarmEscapeGuide fades in and out of them.
    const string StartledState = "SwarmStartled", BackAwayState = "SwarmBackAway";
    const string StartledClip = "Assets/MainCharacter/Khalid_Startled.anim";         // "Reacting Surprised"
    const string BackAwayClip = "Assets/MainCharacter/Khalid_BackAwayFear.anim";     // "Walk Backwards Fear"

    static void EnsureCutsceneStates()
    {
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/MainCharacter/MainCharacterAnim.controller");
        if (ac == null) { Debug.LogWarning("[Swarm] Khalid's controller missing"); return; }
        var sm = ac.layers[0].stateMachine;
        // the first version's cowering clip: gone (user 2026-11-20: "the animation is bad")
        foreach (var c in sm.states) if (c.state.name == "SwarmTerrified") { sm.RemoveState(c.state); break; }
        EnsureState(sm, StartledState, StartledClip, new Vector3(520f, 420f, 0f));
        EnsureState(sm, BackAwayState, BackAwayClip, new Vector3(520f, 480f, 0f));
        EditorUtility.SetDirty(ac);
    }

    static void EnsureState(AnimatorStateMachine sm, string name, string clipPath, Vector3 at)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { Debug.LogWarning("[Swarm] missing " + clipPath); return; }
        AnimatorState st = null;
        foreach (var c in sm.states) if (c.state.name == name) st = c.state;
        if (st == null) st = sm.AddState(name, at);
        st.motion = clip;
        st.writeDefaultValues = true;
    }

    static Transform Find(string name, string parent)
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.scene.IsValid() && go.name == name && go.transform.parent != null && go.transform.parent.name == parent) return go.transform;
        return null;
    }

    static GameObject Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null) throw new System.Exception("[Swarm] '" + name + "' not found under " + parent.name);
        return t.gameObject;
    }

    static Transform DoorOf(DoorLockTrigger t)
    {
        var p = new SerializedObject(t).FindProperty("door").objectReferenceValue as Component;
        return p != null ? p.transform : null;
    }

    static T Load<T>(string path) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) Debug.LogWarning("[Swarm] missing " + path);
        return a;
    }

    static void SetArray(SerializedProperty p, params Object[] items)
    {
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    static void AddCall(SerializedProperty evt, Object target, string method)
    {
        var calls = evt.FindPropertyRelative("m_PersistentCalls.m_Calls");
        for (int i = 0; i < calls.arraySize; i++)
        {
            var c = calls.GetArrayElementAtIndex(i);
            if (c.FindPropertyRelative("m_Target").objectReferenceValue == target && c.FindPropertyRelative("m_MethodName").stringValue == method) return;
        }
        calls.arraySize++;
        var e = calls.GetArrayElementAtIndex(calls.arraySize - 1);
        e.FindPropertyRelative("m_Target").objectReferenceValue = target;
        e.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
        e.FindPropertyRelative("m_MethodName").stringValue = method;
        e.FindPropertyRelative("m_Mode").intValue = 1;          // Void
        e.FindPropertyRelative("m_CallState").intValue = 2;     // Runtime Only
        var args = e.FindPropertyRelative("m_Arguments");
        args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
        args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = "UnityEngine.Object, UnityEngine";
    }

    static void AddSetActive(SerializedProperty evt, GameObject target, bool value)
    {
        var calls = evt.FindPropertyRelative("m_PersistentCalls.m_Calls");
        for (int i = 0; i < calls.arraySize; i++)
        {
            var c = calls.GetArrayElementAtIndex(i);
            if (c.FindPropertyRelative("m_Target").objectReferenceValue == target && c.FindPropertyRelative("m_MethodName").stringValue == "SetActive") return;
        }
        calls.arraySize++;
        var e = calls.GetArrayElementAtIndex(calls.arraySize - 1);
        e.FindPropertyRelative("m_Target").objectReferenceValue = target;
        e.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "UnityEngine.GameObject, UnityEngine";
        e.FindPropertyRelative("m_MethodName").stringValue = "SetActive";
        e.FindPropertyRelative("m_Mode").intValue = 6;          // Bool
        e.FindPropertyRelative("m_CallState").intValue = 2;     // Runtime Only
        var args = e.FindPropertyRelative("m_Arguments");
        args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
        args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = "UnityEngine.Object, UnityEngine";
        args.FindPropertyRelative("m_BoolArgument").boolValue = value;
    }

    static void RemoveCalls(SerializedProperty evt, Object target)
    {
        var calls = evt.FindPropertyRelative("m_PersistentCalls.m_Calls");
        for (int i = calls.arraySize - 1; i >= 0; i--)
            if (calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target").objectReferenceValue == target) calls.DeleteArrayElementAtIndex(i);
    }

    static void SetLine(SerializedProperty p, long id)
    {
        var col = LocalizationEditorSettings.GetStringTableCollection("Subtitles");
        p.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = "GUID:" + col.SharedData.TableCollectionNameGuid.ToString("N");
        p.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = id;
        p.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = "";
    }

    static long Line(string key, string en, string ar)
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
        return entry.Id;
    }

    static void GameUiArabic(string key, string ar)
    {
        var col = LocalizationEditorSettings.GetStringTableCollection("GameUI");
        var entry = col.SharedData.GetEntry(key);
        if (entry == null) { Debug.LogWarning("[Swarm] GameUI key '" + key + "' not found"); return; }
        foreach (var t in col.StringTables)
        {
            if (!t.LocaleIdentifier.Code.StartsWith("ar")) continue;
            string text = ArabicSupport.ArabicFixer.Fix(ar, false, false);
            var e = t.GetEntry(entry.Id);
            if (e == null) t.AddEntry(entry.Id, text); else e.Value = text;
            EditorUtility.SetDirty(t);
        }
    }
}
