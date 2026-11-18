using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// PROTOTYPE - Tools > Prototype > Build Block 2 Wardrobe Cloth Seal (Game scene). User 2026-11-18, kept plain on purpose
/// (no new art, reuse the existing scripts):
///  1. the duct tape Questioner (Alley 3 Gameplay/Action Sequence/QuestionerEnemy) is deleted - it no longer spawns;
///  2. Wardrobe (7) by the dead man in red is wrapped in a thick canvas tarp lashed shut with rope (Blender model
///     Assets/Models/WardrobeCloth, built by AnimationTools/wardrobe_cloth); the duct tape sits in a pocket behind it. Cut:
///     the whole tarp swaps for the slashed one (flaps peeled out) and the tape shows through the hole, ready to pick up;
///  3. the cloth is a second BurnableObstacle set up like the Block 3 one (knife, used from the inventory; E or a hammer
///     hit without cutting = "Tied shut. It won't open.");
///  4. picking up the tape removes the gate key (RemoveItemsOnPickup); the swarm starts on its own
///     (ItemPickupEnemySpawner already watches the inventory for the tape);
///  5. the escape cutscene's end: see EscapeCutsceneBBuilder (version B when the Block 3 cloth is not cut yet).
/// Safe to run again.
/// </summary>
public static class WardrobeClothSealBuilder
{
    const string RootName = "Wardrobe Cloth Seal (prototype)";
    const string RedirectName = "Escape Ending Redirect (prototype)";
    const string MarkerName = "EscapeEnd_AlSarabStreet";
    const string LineKey = "Tied shut. It won't open.";
    const string TapePrefab = "Assets/Prefab/ItemsPickups/DuctTapePickup.prefab";
    const string ModelPath = "Assets/Models/WardrobeCloth/WardrobeCloth.fbx";
    const string MatDir = "Assets/Models/WardrobeCloth/Materials";
    const float TapeDiameter = 0.17f;

    [MenuItem("Tools/Prototype/Build Block 2 Wardrobe Cloth Seal (Game scene)")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!scene.isLoaded) { Debug.LogError("[WardrobeSeal] open the Game scene first"); return; }
        if (EditorApplication.isPlaying) { Debug.LogError("[WardrobeSeal] stop Play mode first"); return; }
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

        Transform actionSeq = all.FirstOrDefault(t => t.name == "Action Sequence" && t.parent != null && t.parent.name == "Alley 3 Gameplay");
        Transform wardrobe = all.FirstOrDefault(t => t.name == "Wardrobe (7)");
        var block3 = all.Select(t => t.GetComponent<BurnableObstacle>()).FirstOrDefault(b => b != null && b.name == "ClothBlockage");
        Transform cutscene = all.FirstOrDefault(t => t.name == "ActionSequenceCutscene");
        Transform spawnPoint = cutscene != null ? cutscene.Find("SpawnPoint") : null;
        var cutTrigger = cutscene != null ? cutscene.GetComponentsInChildren<CutsceneTrigger>(true).FirstOrDefault() : null;
        if (actionSeq == null || wardrobe == null || block3 == null || spawnPoint == null || cutTrigger == null)
        { Debug.LogError("[WardrobeSeal] Action Sequence / Wardrobe (7) / ClothBlockage / ActionSequenceCutscene not found"); return; }

        // ---- 1. the duct tape Questioner: gone (the bloody note and the aggro trigger had it; both skip a missing enemy)
        var questioner = actionSeq.Find("QuestionerEnemy");
        if (questioner != null) { Object.DestroyImmediate(questioner.gameObject); Debug.Log("[WardrobeSeal] deleted the duct tape Questioner"); }

        // ---- 2/3. the seal
        var old = actionSeq.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var box = wardrobe.GetComponent<BoxCollider>();
        // the doors are on the wardrobe's local -x face (looked at from both sides); n = out of the doors
        Vector3 n = -wardrobe.right;
        Vector3 front = wardrobe.TransformPoint(new Vector3(box.center.x - box.size.x * 0.5f, box.center.y, box.center.z));
        Vector3 frontFoot = wardrobe.TransformPoint(new Vector3(box.center.x - box.size.x * 0.5f, box.center.y - box.size.y * 0.5f, box.center.z));
        float floorY = frontFoot.y;      // the wardrobe stands on the ground (its pivot is below it)
        Quaternion faceOut = Quaternion.LookRotation(n, Vector3.up);

        var root = new GameObject(RootName);
        root.transform.SetParent(actionSeq, false);
        root.transform.SetPositionAndRotation(new Vector3(front.x, floorY + 1f, front.z) + n * 0.5f, faceOut);
        var block3Sphere = block3.GetComponent<SphereCollider>();
        var sphere = root.AddComponent<SphereCollider>(); sphere.isTrigger = true; sphere.radius = block3Sphere != null ? block3Sphere.radius : 1.7f;
        var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false;
        var b3Audio = block3.GetComponent<AudioSource>(); if (b3Audio != null) audio.outputAudioMixerGroup = b3Audio.outputAudioMixerGroup;
        root.AddComponent<UniqueID>().GenerateNewID();
        root.AddComponent<SaveableInteractable>();

        // the cloth: a thick canvas tarp lashed round the wardrobe with three ropes (Blender: AnimationTools/wardrobe_cloth).
        // Model origin = middle of the door face at the ground, +Z out of the doors. Cloth_Intact goes when cut; Cloth_Cut
        // (knife X-slash, flaps peeled out) and the tape in its pocket come in together ("Opened"); the ropes stay.
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), scene);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Cloth Model";
        model.transform.SetParent(root.transform, false);
        model.transform.SetPositionAndRotation(frontFoot, faceOut);
        var cloth = model.transform.Find("Cloth_Intact").gameObject;
        var clothCut = model.transform.Find("Cloth_Cut");
        var tapeSpot = model.transform.Find("TapeSpot");
        cloth.GetComponent<Renderer>().sharedMaterial = Mat("WardrobeCloth_Intact", false);
        clothCut.GetComponent<Renderer>().sharedMaterial = Mat("WardrobeCloth_Cut", false);
        model.transform.Find("Ropes").GetComponent<Renderer>().sharedMaterial = Mat("WardrobeRope", true);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var opened = new GameObject("Opened (after the cut)");
        opened.transform.SetParent(root.transform, false);
        opened.transform.SetPositionAndRotation(frontFoot, faceOut);
        clothCut.SetParent(opened.transform, true);

        var cutPos = new GameObject("CuttingPosition").transform;
        cutPos.SetParent(root.transform, false);
        cutPos.SetPositionAndRotation(new Vector3(front.x, floorY, front.z) + n * 0.9f, Quaternion.LookRotation(-n, Vector3.up));

        // "Tied shut. It won't open." - a SubtitleTrigger like the Block 3 cloth's own, single line
        var lineEntryId = Line(LineKey, LineKey, "مربوط. ما ينفتح.");
        var unused = block3.GetComponentsInChildren<SubtitleTrigger>(true).FirstOrDefault(s => s.name == "UnusedSubtitle");
        var lineGo = new GameObject("TiedShutSubtitle");
        lineGo.transform.SetParent(root.transform, false);
        var line = lineGo.AddComponent<SubtitleTrigger>();
        if (unused != null) EditorUtility.CopySerialized(unused, line);
        var lso = new SerializedObject(line);
        lso.FindProperty("subtitleType").enumValueIndex = 0;                       // Single
        lso.FindProperty("subtitleRef.m_TableReference.m_TableCollectionName").stringValue = "GUID:" + SubtitlesCollectionGuid();
        lso.FindProperty("subtitleRef.m_TableEntryReference.m_KeyId").longValue = lineEntryId;
        lso.FindProperty("subtitleRef.m_TableEntryReference.m_Key").stringValue = "";
        lso.FindProperty("subtitleGroupRef").arraySize = 0;
        lso.ApplyModifiedPropertiesWithoutUndo();

        // the duct tape in the pocket behind the cloth, standing on its edge facing out, seen through the cut
        var tape = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TapePrefab), scene);
        // a plain copy: script changes to a prefab instance's name / place / active state are not kept as overrides
        PrefabUtility.UnpackPrefabInstance(tape, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        tape.name = "DuctTapePickup (in wardrobe)";
        tape.transform.SetParent(opened.transform, true);
        tape.transform.SetPositionAndRotation(tapeSpot.position, faceOut * Quaternion.Euler(90f, 0f, 0f));   // the roll's flat side out
        var roll = tape.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "DuctTape");
        if (roll != null)
        {
            // a real roll's size (the pickup's own model is oversized), its middle on the spot
            var e = roll.bounds.extents;
            float across = 2f * Mathf.Max(e.x, Mathf.Max(e.y, e.z));
            roll.transform.localScale *= TapeDiameter / Mathf.Max(0.01f, across);
            tape.transform.position += tapeSpot.position - roll.bounds.center;
        }
        var tapeId = new SerializedObject(tape.GetComponent<UniqueID>());
        tapeId.FindProperty("id").stringValue = System.Guid.NewGuid().ToString();
        tapeId.ApplyModifiedPropertiesWithoutUndo();
        var tapeSphere = tape.GetComponent<SphereCollider>();
        if (tapeSphere != null) tapeSphere.radius = Mathf.Max(tapeSphere.radius, 1.4f / Mathf.Max(0.01f, tape.transform.lossyScale.x));
        opened.SetActive(false);

        // the cut: a second BurnableObstacle set up like the Block 3 cloth
        var seal = root.AddComponent<BurnableObstacle>();
        var b3so = new SerializedObject(block3);
        var so = new SerializedObject(seal);
        so.FindProperty("requiredItemType").enumValueIndex = b3so.FindProperty("requiredItemType").enumValueIndex;   // Knife
        so.FindProperty("consumeItem").boolValue = false;
        so.FindProperty("obstacleObject").objectReferenceValue = cloth;
        so.FindProperty("burnPosition").objectReferenceValue = cutPos;
        so.FindProperty("burnSound").objectReferenceValue = b3so.FindProperty("burnSound").objectReferenceValue;
        so.FindProperty("burnDuration").floatValue = b3so.FindProperty("burnDuration").floatValue;
        so.FindProperty("activateExistingObject").boolValue = true;
        so.FindProperty("replacementObject").objectReferenceValue = opened;
        so.FindProperty("noItemSubtitle").objectReferenceValue = line;
        so.FindProperty("successSubtitle").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        // a hammer hit on the wardrobe or the cloth says the same line
        var hit = root.AddComponent<MeleeHitLine>();
        SetHitLine(hit, seal, line);
        var wardrobeHit = wardrobe.GetComponent<MeleeHitLine>() ?? wardrobe.gameObject.AddComponent<MeleeHitLine>();
        SetHitLine(wardrobeHit, seal, line);

        // ---- 4. tape picked up -> the gate key stays behind
        var removal = root.AddComponent<RemoveItemsOnPickup>();
        var rso = new SerializedObject(removal);
        rso.FindProperty("watchItem").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Item>("Assets/Resources/Items/Key items/DuctTape.asset");
        var rem = rso.FindProperty("itemsToRemove"); rem.arraySize = 1;
        rem.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Item>("Assets/Resources/Items/Key items/Gate Key.asset");
        rso.ApplyModifiedPropertiesWithoutUndo();

        // ---- 5. the escape's end: Tools > Cutscenes > Build Escape Cutscene B (version B of the cutscene when the Block 3
        //      cloth is still there) - it replaced the old Al-Sarab Street redirect

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[WardrobeSeal] built: " + RootName);
    }

    // CustomUnlit (like the wardrobe itself) with the baked-lighting PS2 texture
    static Material Mat(string name, bool rope)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Models/WardrobeCloth", "Materials");
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Custom/CustomUnlit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Custom/CustomUnlit");
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/WardrobeCloth/Textures/" + name + ".png");
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetHitLine(MeleeHitLine hit, BurnableObstacle seal, SubtitleTrigger line)
    {
        var so = new SerializedObject(hit);
        so.FindProperty("obstacle").objectReferenceValue = seal;
        so.FindProperty("line").objectReferenceValue = line;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hit);
    }

    static string SubtitlesCollectionGuid()
    {
        var col = LocalizationEditorSettings.GetStringTableCollection("Subtitles");
        return col.SharedData.TableCollectionNameGuid.ToString("N");
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
}
