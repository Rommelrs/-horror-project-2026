using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tools > Weapons > Build Melee Pipe: the player's steel pipe (see PlayerMeleeWeapon).
///  - pipe model (AnimationTools/melee/build_pipe.py -> Assets/Models/Weapons/Pipe) + material + item "Steel Pipe" + icon
///  - animator: "Melee" layer after UseItem (full body: stance + strafe walks blend tree, and the attack), params
///    MeleeX / MeleeY / MeleeSwingSpeed (attack speed). Clips: AnimationTools/melee/melee_anims_build.py (torch body + baseball two-handed arms / swing).
///    An older "MeleeUpper" layer (axe-pack swing) is removed.
///  - Player.prefab: PlayerMeleeWeapon + the pipe under hand.r (hidden until it's out)
///  - open scene with a QuestionerTestBench (the Gym): "Melee duel" scenario (pipe vs 1 Questioner), started on Play
/// Tools > Weapons > Remove Melee Pipe takes it all back out of the prefab / controller / bench (assets stay).
/// </summary>
public static class MeleePipeBuilder
{
    const string Dir = "Assets/Models/Weapons/Pipe";
    const string FbxPath = Dir + "/Pipe.fbx";
    const string TexPath = Dir + "/PipeAtlas.png";
    const string MatPath = Dir + "/Pipe.mat";
    const string ItemPath = "Assets/Resources/Items/Steel Pipe.asset";
    const string IconPath = "Assets/Sprites/Items/SteelPipeIcon.png";
    const string AudioDir = "Assets/Audio/Melee";
    const string PlayerPrefabPath = "Assets/Prefab/Player/Player.prefab";
    const string ControllerPath = "Assets/MainCharacter/MainCharacterAnim.controller";
    const string ClipDir = "Assets/MainCharacter/";
    const string MoveLayer = "Melee", UpperLayer = "MeleeUpper", CarryLayer = "MeleeCarry";
    const string UpperBodyMask = "Assets/Animations/UseItemUpperBody.mask";
    const string PipeName = "MeleePipe";
    const string DuelName = "Melee duel - pipe vs 1 Questioner (9 m, spec HP 100)";

    // the pipe in hand.r space (metres; hand.r: +Y fingers, +X thumb side, -Z palm side): the shaft runs through the fist,
    // head out past the thumb, like the axe in the Mixamo clips
    static readonly Vector3 GripPos = new Vector3(0.0f, 0.065f, -0.025f);   // = the fist centre grip_bake.py closes the hand round
    static readonly Vector3 GripEuler = new Vector3(0f, 0f, -90f);
    static readonly Vector3 ThumbAxis = new Vector3(0.999f, 0f, 0.047f);   // hand.r space: the thumb line grip_bake.py lays the pipe along
    // the imported mesh lies along -Z (head at -Z, FBX axis conversion): this stands it on +Y, the axis PlayerMeleeWeapon aims
    static readonly Quaternion MeshUp = Quaternion.Euler(90f, 0f, 0f);

    // ground speed (m/s) each strafe clip covers at 1x, measured from its planted feet; the move state plays 0.85x so he
    // walks deliberately (PlayerMeleeWeapon: forward 1.25, sideways 1.15, back 1.0 m/s)
    const float MoveAnimSpeed = 0.85f;
    static readonly Vector2 WalkF = new Vector2(0f, 1.49f), WalkB = new Vector2(0f, -1.17f), WalkL = new Vector2(-1.35f, 0f), WalkR = new Vector2(1.42f, 0f);

    [MenuItem("Tools/Weapons/Build Melee Pipe")]
    public static void Build()
    {
        var tex = HealingItemsBuilder.ImportTexture(TexPath);
        var mesh = HealingItemsBuilder.FbxMesh(FbxPath, "Pipe");
        if (tex == null || mesh == null) { Debug.LogError("[MeleePipeBuilder] Run AnimationTools/melee/build_pipe.py in Blender first"); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, MatPath); }
        mat.shader = Shader.Find("Standard");
        mat.mainTexture = tex;
        mat.SetFloat("_Glossiness", 0.3f);
        mat.SetFloat("_Metallic", 0.15f);
        EditorUtility.SetDirty(mat);

        var item = BuildItem(mesh, mat);
        var attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipDir + "Khalid_Melee_Attack.anim");
        if (attack == null) { Debug.LogError("[MeleePipeBuilder] Melee clips missing - run AnimationTools/melee/melee_anims_build.py + KhalidClipImporter"); return; }
        EnsureLayers(attack);
        BuildOnPlayer(item, mesh, mat, attack);
        BuildDuel(item);
        AssetDatabase.SaveAssets();
        Debug.Log("[MeleePipeBuilder] Melee pipe built");
    }

    [MenuItem("Tools/Weapons/Remove Melee Pipe")]
    public static void Remove()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        var m = root.GetComponent<PlayerMeleeWeapon>();
        if (m != null) Object.DestroyImmediate(m);
        var hand = FindDeep(root.transform, "hand.r");
        var pipe = hand != null ? hand.Find(PipeName) : null;
        if (pipe != null) Object.DestroyImmediate(pipe.gameObject);
        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var layers = new List<AnimatorControllerLayer>(ctrl.layers);
        RemoveLayers(layers);
        ctrl.layers = layers.ToArray();
        EditorUtility.SetDirty(ctrl);

        var bench = Object.FindFirstObjectByType<QuestionerTestBench>();
        if (bench != null)
        {
            Undo.RecordObject(bench, "Remove melee duel");
            int i = bench.scenarios.FindIndex(s => s.giveMelee);
            if (i >= 0)
            {
                var sc = bench.scenarios[i];
                if (sc.playerSpawn != null) Object.DestroyImmediate(sc.playerSpawn.parent != null && sc.playerSpawn.parent.name == "MeleeDuel" ? sc.playerSpawn.parent.gameObject : sc.playerSpawn.gameObject);
                bench.scenarios.RemoveAt(i);
            }
            bench.autoStartScenario = -1;
            EditorUtility.SetDirty(bench);
            EditorSceneManager.MarkSceneDirty(bench.gameObject.scene);
            EditorSceneManager.SaveScene(bench.gameObject.scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[MeleePipeBuilder] Melee pipe removed");
    }

    // ------------------------------------------------------------------ item

    static Item BuildItem(global::UnityEngine.Mesh mesh, Material mat)
    {
        var model = new GameObject("PipeIconModel");
        var part = new GameObject("Pipe");
        part.transform.SetParent(model.transform, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = mat;
        part.transform.localRotation = Quaternion.Euler(0f, 0f, -48f) * MeshUp;
        System.IO.Directory.CreateDirectory("Assets/Sprites/Items");
        var icon = HealingItemsBuilder.SaveIcon(IconPath, HealingItemsBuilder.RenderIcon(model, new Vector3(10f, -20f, 0f)));
        Object.DestroyImmediate(model);

        var item = AssetDatabase.LoadAssetAtPath<Item>(ItemPath);
        if (item == null) { item = ScriptableObject.CreateInstance<Item>(); AssetDatabase.CreateAsset(item, ItemPath); }
        item.itemName = "Steel Pipe";
        item.description = "A length of rusted steel pipe with a heavy fitting on the end. Press 3 to take it out, hold Aim to raise it, Shoot to swing.";
        item.itemIcon = icon;
        item.isStackable = false;
        item.maxStackSize = 1;
        item.itemType = ItemType.Regular;   // not Weapon: Inventory.HasWeapon() means "has a gun"
        EditorUtility.SetDirty(item);
        return item;
    }

    // ------------------------------------------------------------------ animator

    static void RemoveLayers(List<AnimatorControllerLayer> layers)
    {
        for (int i = layers.Count - 1; i >= 0; i--)
            if (layers[i].name == MoveLayer || layers[i].name == UpperLayer || layers[i].name == CarryLayer)
            {
                if (layers[i].stateMachine != null) Object.DestroyImmediate(layers[i].stateMachine, true);
                layers.RemoveAt(i);
            }
    }

    static void EnsureLayers(AnimationClip attack)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        foreach (var (name, def) in new[] { ("MeleeX", 0f), ("MeleeY", 0f), ("MeleeSwingSpeed", 1f), ("MeleeRaiseSpeed", 1f) })
        {
            bool has = false;
            foreach (var p in ctrl.parameters) if (p.name == name) has = true;
            if (!has) ctrl.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = def });
        }
        // leftover blend trees from an earlier build
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            if (o is BlendTree bt && (bt.name == "MeleeMoveTree" || bt.name == "MeleeCarryTree")) Object.DestroyImmediate(bt, true);

        var layers = new List<AnimatorControllerLayer>(ctrl.layers);
        RemoveLayers(layers);
        int insertAt = layers.Count;
        for (int i = 0; i < layers.Count; i++)
            if (layers[i].name == "UseItem") insertAt = i + 1;

        // upper body: the pipe carried upright in both hands while it's out (legs keep the normal locomotion)
        var carry = new AnimatorStateMachine { name = CarryLayer, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(carry, ctrl);
        var carryState = carry.AddState("MeleeCarry");
        // the user's filmed carry: standing / walking / running, on the normal locomotion's Velocity (m/s: walk 1.8, run 3.2)
        var carryTree = new BlendTree
        {
            name = "MeleeCarryTree", blendType = BlendTreeType.Simple1D, blendParameter = "Velocity",
            useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(carryTree, ctrl);
        carryTree.AddChild(Clip("Khalid_Melee_Hold"), 0f);
        // walk + run hold the carry pose steady (her filmed walk sway read as wobbly on him); the locomotion's lean stays (Reattach)
        carryTree.AddChild(Clip("Khalid_Melee_HoldRun"), 1.8f);
        carryTree.AddChild(Clip("Khalid_Melee_HoldRun"), 3.2f);
        carryState.motion = carryTree;
        carry.defaultState = carryState;
        layers.Insert(insertAt++, new AnimatorControllerLayer
        {
            name = CarryLayer, stateMachine = carry, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMask)
        });

        // full body: ready stance + strafe walks, positioned at the ground speed each one covers (so MeleeX / MeleeY are m/s)
        var move = new AnimatorStateMachine { name = MoveLayer, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(move, ctrl);
        var tree = new BlendTree
        {
            name = "MeleeMoveTree",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "MeleeX",
            blendParameterY = "MeleeY",
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        tree.AddChild(Clip("Khalid_Melee_Idle"), Vector2.zero);
        tree.AddChild(Clip("Khalid_Melee_WalkF"), WalkF * MoveAnimSpeed);
        tree.AddChild(Clip("Khalid_Melee_WalkB"), WalkB * MoveAnimSpeed);
        tree.AddChild(Clip("Khalid_Melee_WalkL"), WalkL * MoveAnimSpeed);
        tree.AddChild(Clip("Khalid_Melee_WalkR"), WalkR * MoveAnimSpeed);
        var moveState = move.AddState("MeleeMove");
        moveState.motion = tree;
        moveState.speed = MoveAnimSpeed;
        move.defaultState = moveState;

        // the attack: full body, he plants his feet for it (PlayerMeleeWeapon crossfades in and back out to MeleeMove)
        var attackState = move.AddState("MeleeAttack");
        attackState.motion = attack;
        attackState.speedParameterActive = true;
        attackState.speedParameter = "MeleeSwingSpeed";

        layers.Insert(insertAt, new AnimatorControllerLayer { name = MoveLayer, stateMachine = move, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override });

        // upper body over that: the filmed raise, standing or on the move (PlayerMeleeWeapon plays it and fades the layer)
        var upper = new AnimatorStateMachine { name = UpperLayer, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(upper, ctrl);
        upper.defaultState = upper.AddState("Empty");
        var raise = upper.AddState("MeleeRaise");
        raise.motion = Clip("Khalid_Melee_Raise");
        raise.speedParameterActive = true;
        raise.speedParameter = "MeleeRaiseSpeed";
        layers.Insert(insertAt + 1, new AnimatorControllerLayer
        {
            name = UpperLayer, stateMachine = upper, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMask)
        });
        ctrl.layers = layers.ToArray();
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }

    static AnimationClip Clip(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipDir + name + ".anim");
        if (c == null) Debug.LogError("[MeleePipeBuilder] missing clip " + name);
        return c;
    }

    // ------------------------------------------------------------------ player

    static void BuildOnPlayer(Item item, global::UnityEngine.Mesh mesh, Material mat, AnimationClip attack)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        // fresh component = the script's current tuning defaults (a rebuild resets Inspector tweaks on it)
        var previous = root.GetComponent<PlayerMeleeWeapon>();
        if (previous != null) Object.DestroyImmediate(previous);
        var melee = root.AddComponent<PlayerMeleeWeapon>();
        var weapons = root.GetComponent<PlayerWeaponSystem>();
        var wso = new SerializedObject(weapons);

        // the pipe in his right hand
        var hand = FindDeep(root.transform, "hand.r");
        GameObject pipe = null;
        if (hand == null) Debug.LogWarning("[MeleePipeBuilder] No hand.r bone - no pipe model");
        else
        {
            var old = hand.Find(PipeName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            pipe = new GameObject(PipeName);
            pipe.layer = hand.gameObject.layer;
            pipe.transform.SetParent(hand, false);
            float s = 1f / Mathf.Max(1e-6f, hand.lossyScale.x);   // hand.r lossyScale is 100: bone-local units are 1/100 m
            pipe.transform.localPosition = GripPos * s;
            // GripEuler lays the pipe along the hand's +X; the bake's thumb axis is 2.7 deg off it (towards +Z)
            pipe.transform.localRotation = Quaternion.FromToRotation(Vector3.right, ThumbAxis) * Quaternion.Euler(GripEuler);
            pipe.transform.localScale = Vector3.one * s;
            var part = new GameObject("Pipe");
            part.layer = pipe.layer;
            part.transform.SetParent(pipe.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = mat;
            part.transform.localRotation = MeshUp;
            pipe.SetActive(false);
        }

        InputActionReference weapon3 = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/HorrorInput.inputactions"))
            if (o is InputActionReference r && r.name == "Player/Weapon3") weapon3 = r;
        if (weapon3 == null) Debug.LogWarning("[MeleePipeBuilder] Player/Weapon3 input not found - key 3 will do nothing");

        var so = new SerializedObject(melee);
        so.FindProperty("selectInput").objectReferenceValue = weapon3;
        foreach (var f in new[] { "aimInput", "shootInput", "moveInput", "lookInput" })
            so.FindProperty(f).objectReferenceValue = wso.FindProperty(f).objectReferenceValue;
        so.FindProperty("weaponItem").objectReferenceValue = item;
        so.FindProperty("pipe").objectReferenceValue = pipe;
        so.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>(true);
        so.FindProperty("attackClip").objectReferenceValue = attack;
        so.FindProperty("raiseClip").objectReferenceValue = Clip("Khalid_Melee_Raise");
        so.FindProperty("sparkEffect").objectReferenceValue = wso.FindProperty("metalHitEffect").objectReferenceValue;
        SetClips(so.FindProperty("swingClips"), "PipeSwing_1", "PipeSwing_2", "PipeSwing_3");
        SetClips(so.FindProperty("fleshHitClips"), "PipeHitFlesh_1", "PipeHitFlesh_2", "PipeHitFlesh_3", "PipeHitFlesh_4");
        SetClips(so.FindProperty("wallHitClips"), "PipeHitWall_1", "PipeHitWall_2");
        so.FindProperty("readyClip").objectReferenceValue = Audio("PipeReady");
        // the root master's rest pose in the melee clips (they're baked with it held there) - the carry re-attach needs it
        var hold = Clip("Khalid_Melee_Hold");
        if (hold != null)
        {
            float C(string prop)
            {
                foreach (var b in AnimationUtility.GetCurveBindings(hold))
                    if (b.path.EndsWith("c_root_master.x") && b.propertyName == prop) return AnimationUtility.GetEditorCurve(hold, b).Evaluate(0f);
                return float.NaN;
            }
            var pos = new Vector3(C("m_LocalPosition.x"), C("m_LocalPosition.y"), C("m_LocalPosition.z"));
            var rot = new Quaternion(C("m_LocalRotation.x"), C("m_LocalRotation.y"), C("m_LocalRotation.z"), C("m_LocalRotation.w"));
            bool ok = !float.IsNaN(pos.x) && !float.IsNaN(rot.w);
            so.FindProperty("hasCarryRoot").boolValue = ok;
            if (ok)
            {
                so.FindProperty("carryRootPos").vector3Value = pos;
                so.FindProperty("carryRootRot").quaternionValue = Quaternion.Normalize(rot);
            }
            else Debug.LogWarning("[MeleePipeBuilder] no c_root_master.x curves in Khalid_Melee_Hold - carry re-attach off");
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static AudioClip Audio(string name)
    {
        string path = AudioDir + "/" + name + ".wav";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp != null && !imp.forceToMono) { imp.forceToMono = true; imp.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static void SetClips(SerializedProperty arr, params string[] names)
    {
        arr.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = Audio(names[i]);
    }

    static Transform FindDeep(Transform t, string name)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true))
            if (c.name == name) return c;
        return null;
    }

    // ------------------------------------------------------------------ gym bench

    static void BuildDuel(Item item)
    {
        var bench = Object.FindFirstObjectByType<QuestionerTestBench>();
        if (bench == null) return;
        Undo.RecordObject(bench, "Melee duel");
        bench.meleeItem = item;

        var holder = bench.transform.Find("MeleeDuel");
        if (holder == null)
        {
            holder = new GameObject("MeleeDuel").transform;
            holder.SetParent(bench.transform, false);
        }
        Transform Point(string name, Vector3 pos, float yaw)
        {
            var t = holder.Find(name);
            if (t == null) { t = new GameObject(name).transform; t.SetParent(holder, false); }
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }
        // open field (room 1): he spawns 9 m ahead, facing you
        var spawn = Point("PlayerSpawn", new Vector3(-100f, 0f, 111f), 0f);
        var enemy = Point("EnemySpawn", new Vector3(-100f, 0f, 120f), 180f);

        int idx = bench.scenarios.FindIndex(s => s.giveMelee);
        var sc = idx >= 0 ? bench.scenarios[idx] : new QuestionerTestBench.Scenario();
        sc.name = DuelName;
        sc.kind = QuestionerTestBench.EnemyKind.Questioner;
        sc.watchFor = "Pipe vs one Questioner. 3 = take the pipe out, hold Aim (right mouse) = raise it: WASD strafes, he faces the Questioner. Shoot (left mouse) = one committed swing with a recovery. Does it feel heavy, do hits connect and sound right? F5 restarts, Home also loads this.";
        sc.playerSpawn = spawn;
        sc.enemies = new[] { new QuestionerTestBench.EnemySpawn { point = enemy } };
        sc.startStability = 100;
        sc.giveGun = false;
        sc.giveMelee = true;
        sc.enemyHealth = 100;     // the Questioner spec's starting value (the prefab has 250): 5 pipe hits
        sc.buttonOffset = new Vector3(2.5f, 1.3f, 0f);
        if (idx < 0) { bench.scenarios.Add(sc); idx = bench.scenarios.Count - 1; }
        bench.autoStartScenario = idx;

        EditorUtility.SetDirty(bench);
        EditorSceneManager.MarkSceneDirty(bench.gameObject.scene);
        EditorSceneManager.SaveScene(bench.gameObject.scene);
    }
}
