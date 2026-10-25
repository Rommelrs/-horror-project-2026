using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the flying enemy prototype (FlyingEnemy) out of primitives - a body, an eye, two flapping wings and a weak
// point mount on its belly (seen from below, where the player looks up at it) - saves it as a prefab and puts one in
// the open scene in front of the player. Hit sounds / hit particles are borrowed from the Runner prefab.
public static class FlyingEnemyBuilder
{
    const string Dir = "Assets/Prefab/Enemy/FlyingEnemy";
    const string PrefabPath = Dir + "/FlyingEnemy.prefab";
    const string Runner = "Assets/Prefab/Enemy/RunnerEnemy.prefab";
    const string WeakpointPrefab = "Assets/Prefab/Weakpoint/WeakpointHitbox.prefab";

    [MenuItem("Tools/Enemies/Build Flying Enemy Prototype")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Prefab/Enemy", "FlyingEnemy");
        var bodyMat = Mat("FlyingEnemy_Body", new Color(0.22f, 0.16f, 0.15f), Color.black);
        var wingMat = Mat("FlyingEnemy_Wing", new Color(0.12f, 0.1f, 0.1f), Color.black);
        var eyeMat = Mat("FlyingEnemy_Eye", new Color(0.9f, 0.15f, 0.1f), new Color(2.5f, 0.3f, 0.15f));

        var root = new GameObject("FlyingEnemy");
        root.layer = LayerMask.NameToLayer("Enemy");
        var col = root.AddComponent<SphereCollider>(); col.radius = 0.5f;
        root.AddComponent<Hitbox>().damageType = DamageType.Normal;
        var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 1f;

        var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
        Part(PrimitiveType.Sphere, "Body", model, Vector3.zero, new Vector3(0.9f, 0.65f, 1.1f), bodyMat);
        Part(PrimitiveType.Sphere, "Eye", model, new Vector3(0f, 0.08f, 0.5f), Vector3.one * 0.2f, eyeMat);
        var leftWing = Wing("LeftWing", model, -1f, wingMat);
        var rightWing = Wing("RightWing", model, 1f, wingMat);
        var mount = new GameObject("WeakpointMount").transform;
        mount.SetParent(root.transform, false); mount.localPosition = new Vector3(0f, -0.4f, 0.1f);

        var health = root.AddComponent<Health>();
        var stats = root.AddComponent<Stats>();
        var weak = root.AddComponent<EnemyWeakpoint>();
        weak.weakpointType = EnemyWeakpoint.WeakpointType.Fixed;
        weak.weakpointConfigs = new[] { new WeakpointConfig { boneTransform = mount, weakpointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeakpointPrefab), localScale = Vector3.one * 0.8f } };

        var hso = new SerializedObject(health);
        hso.FindProperty("health").intValue = 120; hso.FindProperty("maxHealth").intValue = 120;
        var runner = AssetDatabase.LoadAssetAtPath<GameObject>(Runner);
        if (runner != null)
        {
            var rh = new SerializedObject(runner.GetComponent<Health>());
            hso.CopyFromSerializedProperty(rh.FindProperty("hitParticleEffect"));
            hso.CopyFromSerializedProperty(rh.FindProperty("hitSoundEffects"));
            stats.takeDamageSFX = runner.GetComponent<Stats>().takeDamageSFX;
        }
        hso.ApplyModifiedPropertiesWithoutUndo();

        var fe = root.AddComponent<FlyingEnemy>();
        fe.health = health; fe.stats = stats; fe.enemyWeakpoint = weak;
        fe.model = model; fe.leftWing = leftWing; fe.rightWing = rightWing;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        // one in the open scene, in front of the player and up in the air
        var old = GameObject.Find("FlyingEnemy");
        if (old != null) Object.DestroyImmediate(old);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var player = Object.FindFirstObjectByType<Player>();
        if (player != null) inst.transform.position = player.transform.position + player.transform.forward * 7f + Vector3.up * 3.5f;
        Undo.RegisterCreatedObjectUndo(inst, "Flying enemy");
        EditorSceneManager.MarkSceneDirty(inst.scene);
        Selection.activeGameObject = inst;
        Debug.Log("[FlyingEnemy] built " + PrefabPath + ", placed at " + inst.transform.position);
    }

    static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    // a pivot at the body's side (the flap turns it) with the flat wing out from it
    static Transform Wing(string name, Transform parent, float side, Material mat)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false); pivot.localPosition = new Vector3(0.35f * side, 0.12f, -0.05f);
        Part(PrimitiveType.Cube, "Membrane", pivot, new Vector3(0.6f * side, 0f, -0.1f), new Vector3(1.15f, 0.04f, 0.6f), mat);
        return pivot;
    }

    static Material Mat(string name, Color color, Color emission)
    {
        string path = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = color;
        m.SetFloat("_Glossiness", 0.2f);
        if (emission != Color.black) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
        EditorUtility.SetDirty(m);
        return m;
    }
}
