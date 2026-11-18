using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Cutscenes > Build Bridge Runner Sequence (Game scene): after the knife handle, walking into the user's drawn area
/// the Runner (Adel on its shoulder) bursts out from behind a parked car, Khalid chases it to the plank bridge over the
/// ravine and it shoves the plank in (BridgeRunnerSequence). Also the cars it hides behind, the Block 1 funnel and the
/// "Pistol pickup" checkpoint on the pistol. Safe to run again.
/// </summary>
public static class BridgeRunnerSequenceBuilder
{
    const string RootName = "Bridge Runner Sequence";
    const string AdelFbx = "Assets/Models/Adel/Adel_ All animations_ In one track.fbx";
    const string AdelController = "Assets/Cutscenes/Kidnap/Adel_Kidnap.controller";
    const string RunnerPrefab = "Assets/Prefab/Enemy/RunnerPuppet.prefab";
    const string ChaseMusic = "Assets/Audio/Glass Table/Music.mp3";     // the park chase's (KidnapSequence)
    const string PlankCreak = "Assets/Audio/Bridge Pass/SubtleMovement.mp3";
    const string PlankDrop = "Assets/Audio/Bridge Pass/BridgeDrop.mp3";
    const string PistolCheckpointName = "Pistol pickup";

    // Where the Runner waits (user 2026-11-18: "make the runner comes from something instead of randomly spawning ... move/add
    // cars"): crouched behind a car parked at the lower kerb by each of the user's R marks (-112.1,-3.2) (-112.3,-30.6)
    // (-112.4,-60.1), on the road side of it, so Khalid coming up from the plaza / car lot / east side can't see it.
    // The R2 car is the road's own Car 4 (1), moved there from z -37; R1 and R3 are copies of the road's Car 2 (the tallest, 2.3 m).
    class Cover
    {
        public string carName;      // existing car under Environment/Cars: moved (copy == false) or copied (copy == true)
        public bool copy;
        public Vector3 carCentre; public float carYaw;
        public Vector3 hide; public float crouch;
        public Vector3[] leadIn;
    }
    static readonly Cover[] Covers =
    {
        new Cover { carName = "Car 2", copy = true, carCentre = new Vector3(-109.9f, 1.5f, -2.8f), carYaw = 92f,   // a bit toward the car lot: Khalid comes from there
                    hide = new Vector3(-107.6f, 1.5f, -1.5f), crouch = 1.0f,
                    leadIn = new[] { new Vector3(-106.9f, 1.5f, -4.6f), new Vector3(-106.2f, 1.5f, -7.0f) } },
        new Cover { carName = "Car 4 (1)", copy = false, carCentre = new Vector3(-109.9f, 1.5f, -30.5f), carYaw = 266f,
                    hide = new Vector3(-107.6f, 1.5f, -30.5f), crouch = 1.2f,
                    leadIn = new[] { new Vector3(-106.3f, 1.5f, -27.5f), new Vector3(-106.0f, 1.5f, -14f), new Vector3(-105.6f, 1.5f, -10.5f) } },
        // at the far road's mouth (z -63), level with where Khalid comes up that road: out of its north end he's in sight at once
        new Cover { carName = "Car 2", copy = true, carCentre = new Vector3(-109.9f, 1.5f, -63.0f), carYaw = 87f,
                    hide = new Vector3(-107.6f, 1.5f, -63.0f), crouch = 1.0f,
                    leadIn = new[] { new Vector3(-106.5f, 1.5f, -59.8f), new Vector3(-105.6f, 1.5f, -56f), new Vector3(-105.0f, 1.5f, -52.0f), new Vector3(-105.8f, 1.5f, -47f),
                                     new Vector3(-106.0f, 1.5f, -14f), new Vector3(-105.6f, 1.5f, -10.5f) } },
    };
    // up the side road, over the plank (z -8.15, between the invisible rails), to the shove spot on the far side
    static readonly Vector3[] Route =
    {
        new Vector3(-104f, 1.5f, -8.3f), new Vector3(-95f, 1.5f, -8.2f),
        new Vector3(-90.6f, 1.5f, -8.15f), new Vector3(-82.7f, 1.7f, -8.15f), new Vector3(-81.4f, 1.5f, -7.95f)
    };
    static readonly Vector3[] Away = { new Vector3(-76f, 1.5f, -7.9f), new Vector3(-70f, 1.5f, -8.3f), new Vector3(-62f, 1.5f, -8.6f) };

    [MenuItem("Tools/Cutscenes/Build Bridge Runner Sequence (Game scene)")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!scene.isLoaded) { Debug.LogError("[BridgeRunner] open the Game scene first"); return; }
        if (EditorApplication.isPlaying) { Debug.LogError("[BridgeRunner] stop Play mode first"); return; }

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        Transform parent = all.FirstOrDefault(t => t.name == "Between Alley 2-3 Gameplay");
        Transform bridge = all.FirstOrDefault(t => t.name == "BrokenBridgeInteractable" && t.parent == parent);
        Transform knife = all.FirstOrDefault(t => t.name == "KnifeHandlePickup");
        if (parent == null || bridge == null || knife == null) { Debug.LogError("[BridgeRunner] Between Alley 2-3 Gameplay / BrokenBridgeInteractable / KnifeHandlePickup not found"); return; }
        Transform bridgePlank = bridge.Find("WoodenPlank");
        if (bridgePlank == null) { Debug.LogError("[BridgeRunner] the bridge's WoodenPlank not found"); return; }

        var old = parent.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(RootName);
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(-110.15f, 1.5f, -23.5f);

        var runnerGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefab), scene);
        runnerGo.name = "Bridge Runner";
        runnerGo.transform.SetParent(root.transform, true);
        runnerGo.transform.position = Ground(Covers[0].hide);
        runnerGo.SetActive(false);

        var adelGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AdelFbx), scene);
        adelGo.name = "Bridge Adel";
        adelGo.transform.SetParent(root.transform, true);
        adelGo.transform.position = Ground(Covers[0].hide) + Vector3.up * 1.5f;
        var adelAnim = adelGo.GetComponent<Animator>();
        if (adelAnim == null) adelAnim = adelGo.AddComponent<Animator>();
        adelAnim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AdelController);
        adelAnim.applyRootMotion = false;
        adelAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        adelGo.SetActive(false);

        // the plank across the gap: a copy of the one the player lays there later (same look, same collider)
        bool wasActive = bridgePlank.gameObject.activeSelf;
        var plankGo = Object.Instantiate(bridgePlank.gameObject, root.transform, true);
        plankGo.name = "Runner Plank";
        plankGo.transform.SetPositionAndRotation(bridgePlank.position, bridgePlank.rotation);
        plankGo.SetActive(false);
        if (bridgePlank.gameObject.activeSelf != wasActive) bridgePlank.gameObject.SetActive(wasActive);

        var seq = root.AddComponent<BridgeRunnerSequence>();
        var so = new SerializedObject(seq);
        so.FindProperty("knifeHandleId").stringValue = knife.GetComponent<UniqueID>() != null ? knife.GetComponent<UniqueID>().ID : "";
        so.FindProperty("bridgeRepairId").stringValue = bridge.GetComponent<UniqueID>() != null ? bridge.GetComponent<UniqueID>().ID : "";
        so.FindProperty("runner").objectReferenceValue = runnerGo.GetComponent<RunnerPuppet>();
        so.FindProperty("adel").objectReferenceValue = adelAnim;
        so.FindProperty("plank").objectReferenceValue = plankGo.transform;
        var cars = BuildCoverCars(all.Where(t => t != null).ToList(), root.transform);
        var spawns = so.FindProperty("spawns");
        spawns.arraySize = Covers.Length;
        for (int i = 0; i < Covers.Length; i++)
        {
            var e = spawns.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("hide").vector3Value = Ground(Covers[i].hide);
            e.FindPropertyRelative("cover").objectReferenceValue = cars[i];
            e.FindPropertyRelative("crouch").floatValue = Covers[i].crouch;
            SetList(e.FindPropertyRelative("leadIn"), Covers[i].leadIn);
        }
        SetList(so.FindProperty("route"), Route);
        SetList(so.FindProperty("away"), Away);
        so.FindProperty("sfxGroup").objectReferenceValue = Group("SFX");
        // no Runner voice here (user), only its footsteps; the park chase's music
        so.FindProperty("musicGroup").objectReferenceValue = Group("Music");
        so.FindProperty("music").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ChaseMusic);
        so.FindProperty("plankCreak").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PlankCreak);
        so.FindProperty("plankDrop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PlankDrop);
        so.ApplyModifiedPropertiesWithoutUndo();

        if (string.IsNullOrEmpty(so.FindProperty("knifeHandleId").stringValue)) Debug.LogError("[BridgeRunner] knife handle has no UniqueID");

        AddPistolCheckpoint(all.Where(t => t != null).ToList());
        BuildFunnel(scene, so.FindProperty("knifeHandleId").stringValue);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BridgeRunner] built: " + root.name + " under " + parent.name);
    }

    /// <summary>The parked cars the Runner hides behind: copies go under the sequence (rebuilt with it), the moved one stays
    /// where it is in Environment/Cars. Placed by their visible middle, wheels on the road.</summary>
    static Transform[] BuildCoverCars(System.Collections.Generic.List<Transform> all, Transform root)
    {
        Transform carsParent = all.FirstOrDefault(t => t.name == "Cars" && t.parent != null && t.parent.name.Contains("Environment"));
        var group = new GameObject("Runner Cover Cars");
        group.transform.SetParent(root, true);
        var result = new Transform[Covers.Length];
        for (int i = 0; i < Covers.Length; i++)
        {
            var c = Covers[i];
            Transform src = carsParent != null ? carsParent.Cast<Transform>().FirstOrDefault(t => t.name == c.carName) : null;
            if (src == null) { Debug.LogError("[BridgeRunner] cover car '" + c.carName + "' not found under Environment/Cars"); continue; }
            Transform car = src;
            if (c.copy)
            {
                car = Object.Instantiate(src.gameObject, group.transform, true).transform;
                car.name = "Cover Car (R" + (i + 1) + ")";
            }
            car.rotation = Quaternion.Euler(270f, c.carYaw, 0f);
            car.position = c.carCentre;
            var rs = car.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float ground = GroundIgnoring(c.carCentre, car).y;
            car.position += new Vector3(c.carCentre.x - b.center.x, ground - b.min.y, c.carCentre.z - b.center.z);
            EditorUtility.SetDirty(car);
            result[i] = car;
        }
        return result;
    }

    // ------------------------------------------------------------------ Block 1 funnel (user, 2026-11-17)
    // "block the main entrance of block 1 ... replace this gate with rubble blockade, and put some tree obstacles in these red
    // lines, this way every player will be forced to the place". The gate is the only way IN, so it is buried only once the
    // knife handle is picked up (user's choice). Checked by a walkability flood fill: with these, every route out of Block 1's
    // doors ends in the Runner's area.
    const string FunnelName = "Block 1 Funnel";
    const string RubblePrefab = "Assets/Prefab/AlleyRubble.prefab";
    static readonly Vector3 GateCentre = new Vector3(-176.6f, 1.5f, 11.7f);
    // (x, z offset from the gate line, yaw, scale)
    static readonly Vector4[] RubblePiles = { new Vector4(-180.2f, 0.4f, 15f, 1.6f), new Vector4(-176.6f, -0.2f, 200f, 1.85f), new Vector4(-173.0f, 0.3f, 95f, 1.6f) };
    // tree lines: from Block 1's fence (x -146.2) to the area's near edge (x -120, moved up 2026-11-18 from -131): plaza edge,
    // and the car lot's right palm row
    const float LineFromX = -146.4f, LineToX = -120f;
    const float PlazaEdgeZ = -6.6f, LotRowZ = -28.5f;
    static readonly float[] PlazaPalmsX = { -145.3f, -143.1f, -140.9f, -138.7f, -136.5f, -134.3f, -132.1f, -129.9f, -127.7f, -125.5f, -123.3f, -121.1f };
    // between the row's own palms (-140, -136, -130, -124.7, -117.4)
    static readonly float[] LotPalmsX = { -145.2f, -142.6f, -138.1f, -133.1f, -127.4f, -122.1f };

    static void BuildFunnel(Scene scene, string knifeId)
    {
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t != null).ToList();
        Transform alley1 = all.FirstOrDefault(t => t.name == "Alley 1 Gameplay");
        Transform gate = all.FirstOrDefault(t => t.name == "STREET GATE" && t.parent != null && t.parent.name == "Environment7");
        Transform entrance = all.FirstOrDefault(t => t.name == "DoorInteractable (Entrance)");
        Transform gateLock = all.FirstOrDefault(t => t.name == "Lock" && t.GetComponent("Lock") != null && Vector3.Distance(t.position, GateCentre) < 4f);
        // the live palm row by the car lot (the scene keeps several hidden environment copies)
        Transform trunk = all.FirstOrDefault(t => t.name == "PalmTree_single_trunk (24)" && t.gameObject.activeInHierarchy);
        Transform palmParent = trunk != null ? trunk.parent : null;
        Transform leaves = palmParent != null ? palmParent.Find("PalmTree_single_leafs_2sided (24)") : null;
        if (alley1 == null || gate == null || entrance == null || trunk == null || leaves == null)
        { Debug.LogError("[BridgeRunner] funnel: Alley 1 Gameplay / STREET GATE / Entrance / palm template not found"); return; }

        var old = alley1.Find(FunnelName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(FunnelName);
        root.transform.SetParent(alley1, false);
        root.transform.position = GateCentre;

        // ---- the gate, buried once the knife handle is picked up
        var rubble = new GameObject("Gate Rubble (after the knife handle)");
        rubble.transform.SetParent(root.transform, false);
        rubble.transform.position = GateCentre;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RubblePrefab);
        foreach (var r in RubblePiles)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(rubble.transform, true);
            go.transform.position = GroundIgnoring(new Vector3(r.x, 1.5f, GateCentre.z + r.y), gate);
            go.transform.rotation = Quaternion.Euler(0f, r.z, 0f);
            go.transform.localScale = prefab.transform.localScale * r.w;
        }
        var wall = new GameObject("Blocker (invisible)");
        wall.transform.SetParent(rubble.transform, false);
        wall.transform.position = GroundIgnoring(GateCentre, gate) + Vector3.up * 2f;
        var wb = wall.AddComponent<BoxCollider>(); wb.size = new Vector3(12f, 4f, 1.8f);
        rubble.SetActive(false);

        var block = root.AddComponent<PickupBlockade>();
        var bso = new SerializedObject(block);
        bso.FindProperty("pickupId").stringValue = knifeId;
        var show = bso.FindProperty("show"); show.arraySize = 1; show.GetArrayElementAtIndex(0).objectReferenceValue = rubble;
        var hideList = new[] { gate.gameObject, entrance.gameObject, gateLock != null ? gateLock.gameObject : null }.Where(g => g != null).ToArray();
        var hide = bso.FindProperty("hide"); hide.arraySize = hideList.Length;
        for (int i = 0; i < hideList.Length; i++) hide.GetArrayElementAtIndex(i).objectReferenceValue = hideList[i];
        bso.ApplyModifiedPropertiesWithoutUndo();

        // ---- tree lines (always there): palms like the car lot's own, an invisible wall along each so there's no gap to slip through
        float templateGround = GroundIgnoring(trunk.position, palmParent).y;
        BuildTreeLine(root.transform, "Tree Line (plaza edge)", PlazaEdgeZ, PlazaPalmsX, trunk, leaves, palmParent, templateGround, 11);
        BuildTreeLine(root.transform, "Tree Line (car lot)", LotRowZ, LotPalmsX, trunk, leaves, palmParent, templateGround, 23);
        Debug.Log("[BridgeRunner] Block 1 funnel built (gate rubble hides " + hideList.Length + " objects after the knife handle)");
    }

    static void BuildTreeLine(Transform root, string name, float z, float[] xs, Transform trunk, Transform leaves, Transform palmParent, float templateGround, int seed)
    {
        var line = new GameObject(name);
        line.transform.SetParent(root, false);
        line.transform.position = new Vector3((LineFromX + LineToX) * 0.5f, 1.5f, z);
        var rng = new System.Random(seed);
        foreach (float x in xs)
        {
            float jz = z + (float)(rng.NextDouble() - 0.5) * 0.5f;
            Vector3 g = GroundIgnoring(new Vector3(x, 1.5f, jz), palmParent);
            if (g.y > 2.3f) g.y = templateGround;          // landed on a wall top / prop: these lots are flat
            var palm = new GameObject("Palm");
            palm.transform.SetParent(line.transform, false);
            palm.transform.position = g;
            float yaw = (float)rng.NextDouble() * 360f, s = 0.92f + (float)rng.NextDouble() * 0.16f;
            foreach (var part in new[] { trunk, leaves })
            {
                var c = Object.Instantiate(part.gameObject, palm.transform, true);
                c.name = part.name.Split('(')[0].Trim();
                c.transform.position = g + (part.position - new Vector3(part.position.x, templateGround, part.position.z));
                c.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * part.rotation;
                c.transform.localScale = part.lossyScale * s;
            }
        }
        var wall = new GameObject("Blocker (invisible)");
        wall.transform.SetParent(line.transform, false);
        wall.transform.position = new Vector3((LineFromX + LineToX) * 0.5f, GroundIgnoring(new Vector3(-138f, 1.5f, z), palmParent).y + 1.5f, z);
        var b = wall.AddComponent<BoxCollider>(); b.size = new Vector3(LineToX - LineFromX, 3f, 0.7f);
    }

    /// <summary>Ground under p, not counting 'ignore' (the gate / the palms) or invisible blockers.</summary>
    static Vector3 GroundIgnoring(Vector3 p, Transform ignore)
    {
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 6f, p.z), Vector3.down, 14f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
            var r = h.collider.GetComponent<Renderer>();
            if (r != null && !r.enabled) continue;
            p.y = h.point.y; break;
        }
        return p;
    }

    /// <summary>A checkpoint the moment the pistol is picked up (user), after the pickup's own subtitle and gun tutorial.</summary>
    static void AddPistolCheckpoint(System.Collections.Generic.List<Transform> all)
    {
        var pickup = all.Select(t => t.GetComponent<InspectableItemPickup>()).FirstOrDefault(p => p != null && p.name == "PistolPickup (place me)");
        Transform cps = all.FirstOrDefault(t => t.name == "CheckPoints");
        if (pickup == null || cps == null) { Debug.LogError("[BridgeRunner] pistol pickup / CheckPoints not found"); return; }

        CheckpointTrigger trig = null;
        foreach (Transform c in cps)
        {
            var ct = c.GetComponent<CheckpointTrigger>();
            if (ct != null && new SerializedObject(ct).FindProperty("checkpointName").stringValue == PistolCheckpointName) trig = ct;
        }
        if (trig == null)
        {
            int n = 1;
            while (cps.Find("CheckPoint_" + n.ToString("00")) != null) n++;
            var go = new GameObject("CheckPoint_" + n.ToString("00"));
            go.transform.SetParent(cps, false);
            go.transform.position = pickup.transform.position;
            trig = go.AddComponent<CheckpointTrigger>();
            var cso = new SerializedObject(trig);
            cso.FindProperty("checkpointName").stringValue = PistolCheckpointName;
            cso.FindProperty("triggerOnce").boolValue = true;
            cso.FindProperty("triggerOnPlayerEnter").boolValue = false;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }
        for (int i = 0; i < pickup.OnInteracted.GetPersistentEventCount(); i++)
            if (pickup.OnInteracted.GetPersistentTarget(i) == trig) return;
        UnityEventTools.AddPersistentListener(pickup.OnInteracted, trig.Trigger);
        EditorUtility.SetDirty(pickup);
    }

    static void SetList(SerializedProperty prop, Vector3[] values)
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).vector3Value = values[i];
    }

    static Vector3 Ground(Vector3 p)
    {
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 2.5f, p.z), Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            var r = h.collider.GetComponent<Renderer>();
            if (r != null && !r.enabled) continue;     // invisible blockers
            p.y = h.point.y; break;
        }
        return p;
    }

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
}
