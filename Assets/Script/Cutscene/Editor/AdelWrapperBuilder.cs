using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// "Alley 3 Gameplay/Adel Wrapper Drop": the barber candy wrapper Adel drops as he runs off in the table-knockdown
/// pre-cutscene, rebuilt like the old (deleted) CandyWrapperInteractable: ImageFadeInteractable showing the same picture
/// (GameCanvas/Menus/CandyWrapper, "Barber candy wallpaper.jpg"), its SubtitleTrigger - now Khalid's "Emerald Saloon?" -
/// AutoInteractCutscene (walks him over when he comes near) and, on close, the barber map mark + map hint like before.
/// The wrapper's spot = where it lands when it leaves Adel's hand at dropTime (sampled from the timeline). Re-runnable.
/// </summary>
public static class AdelWrapperBuilder
{
    const string RootName = "Adel Wrapper Drop";
    const string Line = "Emerald Saloon?";
    const string LineAr = "صالون الزمرد؟";
    const float DropTime = 3.1f;
    static readonly Vector3 ReleaseVelocity = new Vector3(3.2f, 0.6f, 0f);

    [MenuItem("Tools/Cutscenes/Build Adel Wrapper Drop (Game scene)")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!scene.isLoaded) { Debug.LogError("[Wrapper] open the Game scene first"); return; }
        if (EditorApplication.isPlaying) { Debug.LogError("[Wrapper] stop Play mode first"); return; }
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var alley3 = all.First(t => t.name == "Alley 3 Gameplay");
        var pre = all.First(t => t.name == "KnockdownPreCutscene");
        var director = pre.GetComponent<PlayableDirector>();
        var adel = pre.Find("AdelPos/Adel_ All animations_ In one track");
        var hand = adel.GetComponentsInChildren<Transform>(true).First(t => t.name == "RightLowerArm.001");
        var knockdown = all.First(t => t.name == "KnockDownTrigger").GetComponent<CinematicKnockdownSequence>();
        var panel = all.First(t => t.name == "CandyWrapper" && t.parent != null && t.parent.name == "Menus").gameObject;
        var switcher = all.First(t => t.name == "MenuPanelSwitcher").GetComponent<MenuPanelSwitcher>();
        var reveal = all.First(t => t.name == "MapLocationReveal (1)").GetComponent<MapLocationReveal>();
        var hint = all.First(t => t.name == "MapHintPrompt").GetComponent<MapHintPrompt>();

        // where his hand is at the drop, then the same fall AdelWrapperDrop does at runtime
        // (evaluating the timeline in the editor moves AdelPos, Adel and every bone: snapshot them all and put them back)
        bool wasActive = pre.gameObject.activeSelf;
        var snap = pre.GetComponentsInChildren<Transform>(true).Select(t => (t, t.localPosition, t.localRotation, t.localScale, t.gameObject.activeSelf)).ToList();
        pre.gameObject.SetActive(true);
        director.RebuildGraph(); director.time = DropTime; director.Evaluate();
        Vector3 pos = hand.position;
        director.Stop();
        foreach (var (t, lp, lr, ls, act) in snap) { t.localPosition = lp; t.localRotation = lr; t.localScale = ls; if (t != pre) t.gameObject.SetActive(act); }
        pre.gameObject.SetActive(wasActive);
        float groundY = Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out RaycastHit g, 5f, ~0, QueryTriggerInteraction.Ignore) ? g.point.y : pos.y - 0.7f;
        Vector3 vel = ReleaseVelocity; float dt = 1f / 60f;
        while (pos.y > groundY + 0.02f) { vel.y -= 9.81f * dt; vel.x *= 1f - 0.6f * dt; vel.z *= 1f - 0.6f * dt; pos += vel * dt; }
        Vector3 rest = new Vector3(pos.x + 0.25f, groundY, pos.z + 0.05f);   // a short skid on

        var old = alley3.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(RootName);
        root.transform.SetParent(alley3, false);
        root.transform.SetPositionAndRotation(rest, Quaternion.identity);

        // the wrapper model (the old one's FBX and its import-space fix)
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Candy Wrapper/CANDY WRAPPER MODEL.fbx");
        var visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(root.transform, false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, visualRoot);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = new Quaternion(0.0072361724f, 0.66551214f, 0.74632406f, -0.0064526396f);
        model.transform.localScale = new Vector3(0.000512019f, 0.000603891f, 0.0009f);
        visualRoot.rotation = Quaternion.Euler(0f, 37f, 0f);
        var mb = model.GetComponentInChildren<Renderer>().bounds;
        model.transform.position += new Vector3(rest.x - mb.center.x, rest.y + 0.004f - mb.min.y, rest.z - mb.center.z);

        // where Khalid stops to pick it up: on his side (west), facing it
        var walkTo = new GameObject("WalkToPosition").transform;
        walkTo.SetParent(root.transform, false);
        walkTo.position = rest + new Vector3(-0.85f, 0.25f, -0.1f);

        var col = root.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 3.13f;

        // the line
        var coll = LocalizationEditorSettings.GetStringTableCollection("Subtitles");
        var entry = coll.SharedData.GetEntry(Line) ?? coll.SharedData.AddKey(Line);
        foreach (var table in coll.StringTables)
        {
            string text = table.LocaleIdentifier.Code.StartsWith("ar") ? LineAr : Line;
            var e = table.GetEntry(entry.Id);
            if (e == null) table.AddEntry(entry.Id, text); else e.Value = text;
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(coll.SharedData);

        var sub = root.AddComponent<SubtitleTrigger>();
        var sso = new SerializedObject(sub);
        sso.FindProperty("lifeTime").floatValue = 3f;
        sso.FindProperty("freezeGame").boolValue = true;
        sso.FindProperty("subtitleType").enumValueIndex = 1;   // Group, like the old one
        var grp = sso.FindProperty("subtitleGroupRef"); grp.arraySize = 1;
        var g0 = grp.GetArrayElementAtIndex(0);
        g0.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = "GUID:" + coll.SharedData.TableCollectionNameGuid.ToString("N");
        g0.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = entry.Id;
        sso.ApplyModifiedPropertiesWithoutUndo();

        var img = root.AddComponent<ImageFadeInteractable>();
        var iso = new SerializedObject(img);
        iso.FindProperty("imagePanel").objectReferenceValue = panel;
        iso.FindProperty("menuPanelSwitcher").objectReferenceValue = switcher;
        iso.FindProperty("panelImage").objectReferenceValue = panel.GetComponent<UnityEngine.UI.Image>();
        iso.FindProperty("subtitleTrigger").objectReferenceValue = sub;
        iso.FindProperty("activateOnClose").boolValue = false;
        iso.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddVoidPersistentListener(img.onImageClosed, new UnityAction(reveal.TriggerReveal));
        UnityEventTools.AddVoidPersistentListener(img.onImageClosed, new UnityAction(hint.ShowHint));

        var auto = root.AddComponent<AutoInteractCutscene>();
        var aso = new SerializedObject(auto);
        aso.FindProperty("walkToPosition").objectReferenceValue = walkTo;
        aso.ApplyModifiedPropertiesWithoutUndo();

        var drop = root.AddComponent<AdelWrapperDrop>();
        var dso = new SerializedObject(drop);
        dso.FindProperty("director").objectReferenceValue = director;
        dso.FindProperty("adelHand").objectReferenceValue = hand;
        dso.FindProperty("dropTime").floatValue = DropTime;
        dso.FindProperty("visual").objectReferenceValue = visualRoot;
        dso.FindProperty("knockdown").objectReferenceValue = knockdown;
        dso.FindProperty("releaseVelocity").vector3Value = ReleaseVelocity;
        dso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Wrapper] built: rest " + rest.ToString("F2") + ", hand at drop " + hand.position.ToString("F2"));
    }
}
