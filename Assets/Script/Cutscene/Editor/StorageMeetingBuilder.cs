using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts the storage room meeting (StorageMeetingCutscene) into the Store scene: the trigger inside the door, the cashier
/// puppet at the rack, the carton, all references, the subtitle lines. Re-run after changing anything here, then save.
/// </summary>
public static class StorageMeetingBuilder
{
    const string RootName = "StorageMeeting";

    static readonly string[,] Lines = {
        { "Peace be upon you.", "السلام عليكم" },
        { "Friend, have you seen a boy with a backpack come through here?", "الغالي شفت ولد معاه شنطه مر من هنا؟" },
        { "Alright... thanks anyway.", "تمام يعطيك العافيه" },
        { "Where is he?", "وينه؟" },
        { "Get away from me! Get away!", "بعد عني بعد عني!" },
    };

    [MenuItem("Tools/Cutscenes/Build Storage Meeting")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Store.unity");
        if (!scene.isLoaded) { Debug.LogError("[StorageMeeting] open the Store scene first"); return; }

        Transform gameplay = null; GameObject enemyTimeline = null, hammer = null, entryWhisper = null;
        CheckpointTrigger checkpoint = null; GunTutorialPrompts tutorial = null; AudioMixerGroup sfx = null;
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name.Contains("Gameplay")) gameplay = r.transform;
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "EnemyTimeline1") enemyTimeline = t.gameObject;
                if (t.name == "ItemPickup (Hammer)") hammer = t.gameObject;
                if (t.name == "AudioTrigger" && t.parent != null && t.parent.name == "DoorInteractable (ShopToStorage)") entryWhisper = t.gameObject;
            }
            foreach (var c in r.GetComponentsInChildren<CheckpointTrigger>(true)) if (c.name == "CheckPoint_02") checkpoint = c;
            foreach (var g in r.GetComponentsInChildren<GunTutorialPrompts>(true)) tutorial = g;
            foreach (var fx in r.GetComponentsInChildren<CashRegisterPowerFX>(true))
                sfx = new SerializedObject(fx).FindProperty("sfxGroup").objectReferenceValue as AudioMixerGroup;
        }
        if (gameplay == null || enemyTimeline == null) { Debug.LogError("[StorageMeeting] Gameplay root / EnemyTimeline1 not found"); return; }

        var old = gameplay.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject(RootName);
        root.transform.SetParent(gameplay, false);
        root.transform.position = new Vector3(-218.2f, 2.5f, 92.85f);
        var trig = root.AddComponent<BoxCollider>();
        trig.isTrigger = true; trig.size = new Vector3(2.8f, 2.2f, 1.9f);
        var cs = root.AddComponent<StorageMeetingCutscene>();

        // the cashier
        var puppetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/QuestionerPuppet.prefab");
        var puppet = (GameObject)PrefabUtility.InstantiatePrefab(puppetPrefab, root.transform);
        puppet.name = "Cashier";
        puppet.transform.SetPositionAndRotation(new Vector3(-219.6f, 1.445f, 87.56f), Quaternion.Euler(0f, 180f, 0f));
        var jaw = puppet.AddComponent<QuestionerJawPose>();
        foreach (var t in puppet.GetComponentsInChildren<Transform>(true)) if (t.name == "Jaw") jaw.jaw = t;

        // the carton
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Carton";
        Object.DestroyImmediate(box.GetComponent<Collider>());
        box.transform.SetParent(root.transform, false);
        box.transform.localScale = new Vector3(0.4f, 0.26f, 0.3f);
        box.transform.position = new Vector3(-219.6f, 2.65f, 87.13f);
        var card = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StoreMakeover/SM_FitCardboard.mat");
        if (card != null) box.GetComponent<MeshRenderer>().sharedMaterial = card;

        var so = new SerializedObject(cs);
        so.FindProperty("puppet").objectReferenceValue = puppet;
        so.FindProperty("jaw").objectReferenceValue = jaw;
        so.FindProperty("box").objectReferenceValue = box.transform;
        so.FindProperty("enemy").objectReferenceValue = enemyTimeline.GetComponentInChildren<QuestionerEnemy>(true);
        so.FindProperty("hammerOnShelf").objectReferenceValue = hammer;
        so.FindProperty("hammerItem").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Item>("Assets/Resources/Items/Hammer.asset");
        so.FindProperty("checkpoint").objectReferenceValue = checkpoint;
        so.FindProperty("tutorial").objectReferenceValue = tutorial;
        so.FindProperty("sfxGroup").objectReferenceValue = sfx;
        so.FindProperty("boxSlide").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Store/StoragePresence/BoxDrag_1.wav");
        so.FindProperty("boxDrop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Store/FuseHunt/StorageThump_1.wav");
        so.FindProperty("shelfBump").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Store/StoragePresence/ShelfKnock_2.wav");
        so.FindProperty("whereIsHe").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/StoreRoomSFX/Weenah, close up of the monster.mp3");
        if (hammer != null) so.FindProperty("hammerSpot").vector3Value = hammer.transform.position;
        so.ApplyModifiedPropertiesWithoutUndo();

        // the whisper that used to play on walking in would give him away
        if (entryWhisper != null && entryWhisper.activeSelf) entryWhisper.SetActive(false);

        AddLines();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[StorageMeeting] built (enemy " + (so.FindProperty("enemy").objectReferenceValue != null) + ", hammer " + (hammer != null) + ", checkpoint " + (checkpoint != null) + ", tutorial " + (tutorial != null) + ")");
    }

    static void AddLines()
    {
        var col = LocalizationEditorSettings.GetStringTableCollection("Subtitles");
        if (col == null) { Debug.LogError("[StorageMeeting] Subtitles table not found"); return; }
        for (int i = 0; i < Lines.GetLength(0); i++)
        {
            string en = Lines[i, 0], ar = ArabicSupport.ArabicFixer.Fix(Lines[i, 1], false, false);   // the game shows Arabic pre-shaped
            var entry = col.SharedData.GetEntry(en) ?? col.SharedData.AddKey(en);
            foreach (var table in col.StringTables)
            {
                string text = table.LocaleIdentifier.Code.StartsWith("ar") ? ar : en;
                var e = table.GetEntry(entry.Id);
                if (e == null) table.AddEntry(entry.Id, text); else e.Value = text;
                EditorUtility.SetDirty(table);
            }
        }
        EditorUtility.SetDirty(col.SharedData);
        AssetDatabase.SaveAssets();
    }
}
