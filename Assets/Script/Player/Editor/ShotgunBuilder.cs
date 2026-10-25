using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

// Prototype sawed-off double-barrel shotgun for the player (2026-10-25, built in the Gym). Short barrels and a pistol
// grip, so it sits in the pistol's one-handed hold and reuses the pistol's aim / shoot / reload animations. Two shells,
// a cone of pellets (PlayerWeaponSystem.FirePellets), a heavy boom and a camera kick.
//   Tools > Weapons > Build Shotgun On Player (open scene):
//   - meshes (Assets/Models/Shotgun: the gun, built in the pistol's local frame - barrel along -Z, bore at y 0.127 - so
//     it drops into the same hand bones; a shell), PS2-res texture + white icons (AnimationTools/shotgun/
//     make_shotgun_textures.py), the shot sound (Assets/Audio/Shotgun/ShotgunBlast.wav, ffmpeg mix of the pistol shot
//     pitched down + an explosion's low tail);
//   - items: Resources/Items/Shotgun (Weapon), Resources/Items/Supplies/Shotgun Shells (Ammo);
//   - the shotgun model next to the pistol model in both hands, a PlayerWeaponSwitcher on the Player (1 = pistol,
//     2 = shotgun, d-pad left / right), a spread ring round the crosshair (SpreadCrosshair), the Gym bench's Shift+F8,
//     and a shotgun + shells pickup in front of the Player. Enemies hit by the blast are shoved back
//     (PlayerWeaponSystem.pelletKnockback -> Enemy.pendingKnockbackScale).
//   Rebuilding replaces what it made before.
public static class ShotgunBuilder
{
    const string ModelDir = "Assets/Models/Shotgun";
    const string TexPath = "Assets/Textures/Shotgun/Shotgun.png";
    const string MatPath = "Assets/Textures/Shotgun/Shotgun.mat";
    const string IconPath = "Assets/Sprites/Items/ShotgunIcon.png";
    const string ShellsIconPath = "Assets/Sprites/Items/ShotgunShellsIcon.png";
    const string BlastPath = "Assets/Audio/Shotgun/ShotgunBlast.wav";
    const string RingPath = "Assets/Sprites/ShotgunSpreadRing.png";
    const string ShotgunItemPath = "Assets/Resources/Items/Shotgun.asset";
    const string ShellsItemPath = "Assets/Resources/Items/Supplies/Shotgun Shells.asset";
    const string PistolItemPath = "Assets/Resources/Items/Pistol.asset";
    const string InputPath = "Assets/HorrorInput.inputactions";
    const string ModelName = "SawedOffShotgun";

    // where the muzzles are, in the hand gun's local frame (the pistol's own PistolEndPoint position, and the shotgun's)
    static readonly Vector3 PistolMuzzle = new Vector3(0f, 0.123f, -0.138f);
    static readonly Vector3 ShotgunMuzzle = new Vector3(0f, 0.127f, -0.405f);

    const int Metal = 0, Wood = 1, Red = 2, Brass = 3;    // bands of Shotgun.png, v from the bottom

    public static string LastReport = "";

    [MenuItem("Tools/Weapons/Build Shotgun On Player")]
    public static void Build()
    {
        var weapons = Object.FindObjectOfType<PlayerWeaponSystem>(true);
        if (weapons == null) { Debug.LogError("[ShotgunBuilder] no PlayerWeaponSystem in the open scene"); return; }
        var report = new List<string>();

        ImportSettings();
        var mat = Material(weapons);
        var gunMesh = SaveMesh(BuildGun(), "SawedOffShotgun");
        var shellMesh = SaveMesh(BuildShell(), "ShotgunShell");
        var shotgunItem = MakeItem(ShotgunItemPath, "Shotgun", IconPath, "Sawed-off double barrel. Two shells, a lot of lead.", false, 1, ItemType.Weapon);
        var shellsItem = MakeItem(ShellsItemPath, "Shotgun Shells", ShellsIconPath, "Ammo used in the shotgun.", true, 1000, ItemType.Ammo);
        var pistolItem = AssetDatabase.LoadAssetAtPath<Item>(PistolItemPath);

        // the hand guns: first person (the one gunEnd hangs off) and third person (the enabler's weaponObj). Each is a
        // "Pistol 2" with a nested "Pistol 2"; the shotgun goes beside the pistol's parts (under the Barrel's parent).
        var fpGun = weapons.gunEnd.parent.parent;
        var enabler = weapons.GetComponent<PlayerWeaponEnabler>();
        var tpObj = enabler != null ? new SerializedObject(enabler).FindProperty("weaponObj").objectReferenceValue as GameObject : null;
        var pistolRenderers = new List<Renderer>();
        var shotgunRenderers = new List<Renderer>();
        var muzzles = new List<Transform>();
        foreach (var gun in new[] { fpGun, tpObj != null ? tpObj.transform : null })
        {
            if (gun == null) continue;
            foreach (var old in gun.GetComponentsInChildren<Transform>(true).Where(t => t.name == ModelName).ToList())
                Object.DestroyImmediate(old.gameObject);
            var muzzle = gun.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("PistolEndPoint"));
            if (muzzle != null) { muzzles.Add(muzzle); muzzle.localPosition = PistolMuzzle; }
            var parts = gun.GetComponentsInChildren<MeshRenderer>(true).Where(r => muzzle == null || !r.transform.IsChildOf(muzzle)).ToList();
            foreach (var r in parts) r.enabled = true;
            pistolRenderers.AddRange(parts);
            var barrel = parts.FirstOrDefault(r => r.name == "Barrel");
            var root = barrel != null ? barrel.transform.parent : gun;

            var go = new GameObject(ModelName);
            go.transform.SetParent(root, false);
            go.layer = root.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = gunMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.enabled = false;
            shotgunRenderers.Add(mr);
            report.Add("model under " + root.parent.name + "/" + root.name);
        }

        // the switcher
        var sw = weapons.GetComponent<PlayerWeaponSwitcher>();
        if (sw == null) sw = Undo.AddComponent<PlayerWeaponSwitcher>(weapons.gameObject);
        var refs = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>().ToArray();
        InputActionReference Ref(string action) => refs.FirstOrDefault(r => r.action != null && r.action.name == action);
        var pullOut = new SerializedObject(weapons).FindProperty("pullOutGunClip").objectReferenceValue as AudioClip;
        sw.guns = new[]
        {
            new PlayerWeaponSwitcher.Gun
            {
                name = "Pistol", weaponItem = pistolItem, ammoItem = weapons.weaponAmmoItem, selectInput = Ref("Weapon1"),
                magazine = weapons.maxAmmoCapacity, damage = weapons.damage, fireRate = weapons.fireRate, range = weapons.weaponRange,
                bulletSpread = weapons.bulletSpread, pierce = weapons.maxBulletPierceCount, reloadDuration = weapons.reloadDuration,
                pellets = 1, ejectsCartridge = true, kick = 0f, shootClip = null, shootVolume = 0.8f,
                renderers = pistolRenderers.ToArray(), muzzleLocalPosition = PistolMuzzle,
            },
            new PlayerWeaponSwitcher.Gun
            {
                name = "Shotgun", weaponItem = shotgunItem, ammoItem = shellsItem, selectInput = Ref("Weapon2"),
                magazine = 2, damage = 140, fireRate = 0.35f, range = 25f, bulletSpread = 0.01f, pierce = 1, reloadDuration = 2.6f,
                pellets = 9, pelletSpread = 0.12f, ejectsCartridge = false, kick = 12f,
                shootClip = AssetDatabase.LoadAssetAtPath<AudioClip>(BlastPath), shootVolume = 0.7f,
                renderers = shotgunRenderers.ToArray(), muzzleLocalPosition = ShotgunMuzzle,
            },
        };
        sw.muzzlePoints = muzzles.ToArray();
        sw.switchClip = pullOut;
        EditorUtility.SetDirty(sw);
        report.Add("switcher: inputs " + (sw.guns[0].selectInput != null) + "/" + (sw.guns[1].selectInput != null) + ", " + pistolRenderers.Count + " pistol renderers, " + muzzles.Count + " muzzle points");

        // the spread ring round the crosshair dot
        var dot = new SerializedObject(weapons).FindProperty("aimCrosshairImage").objectReferenceValue as UnityEngine.UI.Image;
        if (dot != null)
        {
            var oldRing = dot.transform.Find("SpreadRing");
            if (oldRing != null) Object.DestroyImmediate(oldRing.gameObject);
            var ringGo = new GameObject("SpreadRing", typeof(RectTransform));
            ringGo.layer = dot.gameObject.layer;
            ringGo.transform.SetParent(dot.transform, false);
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(200f, 200f);
            var ring = ringGo.AddComponent<UnityEngine.UI.Image>();
            ring.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
            ring.raycastTarget = false;
            ring.color = new Color(dot.color.r, dot.color.g, dot.color.b, 0f);
            var spread = dot.GetComponent<SpreadCrosshair>();
            if (spread == null) spread = dot.gameObject.AddComponent<SpreadCrosshair>();
            spread.dot = dot;
            spread.ring = ring;
            EditorUtility.SetDirty(spread);
            report.Add("spread ring under " + dot.name);
        }

        // the Gym bench's Shift+F8
        var bench = Object.FindObjectOfType<QuestionerTestBench>(true);
        if (bench != null)
        {
            Undo.RecordObject(bench, "Shotgun");
            bench.shotgunItem = shotgunItem;
            bench.shellsItem = shellsItem;
            EditorUtility.SetDirty(bench);
            report.Add("bench Shift+F8 wired");
        }

        // pickups in front of the player
        var player = weapons.transform;
        foreach (var n in new[] { "Shotgun Pickup", "Shotgun Shells Pickup" })
        {
            var old = GameObject.Find(n);
            if (old != null) Object.DestroyImmediate(old);
        }
        Vector3 ahead = player.position + player.forward * 2.2f;
        MakePickup("Shotgun Pickup", shotgunItem, 1, Ground(ahead - player.right * 0.4f), player, new[] { (gunMesh, new Vector3(0f, 0.025f, 0f), Quaternion.Euler(0f, 90f, 90f)) }, mat);
        var shells = Enumerable.Range(0, 4).Select(i => (shellMesh, new Vector3(-0.06f + i * 0.04f, 0.011f, (i % 2) * 0.02f), Quaternion.Euler(90f, 0f, 15f * i))).ToArray();
        MakePickup("Shotgun Shells Pickup", shellsItem, 12, Ground(ahead + player.right * 0.5f), player, shells, mat);
        report.Add("pickups at " + ahead.ToString("F1"));

        EditorSceneManager.MarkSceneDirty(weapons.gameObject.scene);
        AssetDatabase.SaveAssets();
        LastReport = string.Join("\n", report);
        Debug.Log("[ShotgunBuilder] built\n" + LastReport);
    }

    // ── assets ──

    static void ImportSettings()
    {
        AssetDatabase.ImportAsset(TexPath); AssetDatabase.ImportAsset(IconPath); AssetDatabase.ImportAsset(ShellsIconPath); AssetDatabase.ImportAsset(BlastPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        ti.filterMode = FilterMode.Point; ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
        AssetDatabase.ImportAsset(RingPath);
        foreach (var p in new[] { IconPath, ShellsIconPath, RingPath })
        {
            var ii = (TextureImporter)AssetImporter.GetAtPath(p);
            ii.textureType = TextureImporterType.Sprite; ii.spriteImportMode = SpriteImportMode.Single;
            ii.alphaIsTransparency = true; ii.mipmapEnabled = false; ii.SaveAndReimport();
        }
        var ai = (AudioImporter)AssetImporter.GetAtPath(BlastPath);
        var s = ai.defaultSampleSettings; s.loadType = AudioClipLoadType.DecompressOnLoad; ai.defaultSampleSettings = s;
        ai.SaveAndReimport();
    }

    // same shader as the pistol, so the gun renders the same way in the first-person view
    static Material Material(PlayerWeaponSystem weapons)
    {
        var pistolMat = weapons.gunEnd.parent.parent.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.sharedMaterial).FirstOrDefault(m => m != null);
        var shader = pistolMat != null ? pistolMat.shader : Shader.Find("Legacy Shaders/Diffuse");
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, MatPath); }
        m.shader = shader;
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        if (m.HasProperty("_Color")) m.color = Color.white;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.25f);     // worn, not polished
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.2f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Item MakeItem(string path, string name, string icon, string description, bool stackable, int maxStack, ItemType type)
    {
        var item = AssetDatabase.LoadAssetAtPath<Item>(path);
        if (item == null) { item = ScriptableObject.CreateInstance<Item>(); AssetDatabase.CreateAsset(item, path); }
        item.itemName = name;
        item.itemIcon = AssetDatabase.LoadAssetAtPath<Sprite>(icon);
        item.description = description;
        item.isStackable = stackable;
        item.maxStackSize = maxStack;
        item.itemType = type;
        EditorUtility.SetDirty(item);
        return item;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        System.IO.Directory.CreateDirectory(ModelDir);
        string path = ModelDir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { mesh.name = name; AssetDatabase.CreateAsset(mesh, path); return mesh; }
        existing.Clear();
        existing.SetVertices(mesh.vertices); existing.SetNormals(mesh.normals); existing.SetUVs(0, mesh.uv); existing.SetTriangles(mesh.triangles, 0);
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static void MakePickup(string name, Item item, int quantity, Vector3 at, Transform player, (Mesh mesh, Vector3 pos, Quaternion rot)[] parts, Material mat)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Shotgun pickup");
        go.transform.position = at;
        go.transform.rotation = Quaternion.Euler(0f, player.eulerAngles.y + 20f, 0f);
        var pickup = go.AddComponent<ItemPickup>();
        pickup.itemToPickup = item;
        pickup.itemPickupQuantity = quantity;
        pickup.interactionType = InteractionType.Pickup;
        pickup.destroyOnInteract = true;
        pickup.interactPoint = go.transform;
        var trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.5f, 0f);
        trigger.size = new Vector3(1.4f, 1f, 1.4f);
        int k = 0;
        foreach (var (mesh, pos, rot) in parts)
        {
            var v = new GameObject("Model " + k++);
            v.transform.SetParent(go.transform, false);
            v.transform.localPosition = pos;
            v.transform.localRotation = rot;
            v.AddComponent<MeshFilter>().sharedMesh = mesh;
            v.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }

    static Vector3 Ground(Vector3 p)
    {
        foreach (var hit in Physics.RaycastAll(p + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            if (hit.collider.GetComponentInParent<Player>() == null) return hit.point;
        return p;
    }

    // ── meshes ──
    // In the hand gun's frame: x right, y up, the barrel along -z (pistol: grip round z +0.05, bore at y 0.123).

    class Builder
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int band, float len)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(normal);
            float v0 = band * 0.25f + 0.02f, v1 = v0 + 0.21f, u1 = Mathf.Max(0.05f, len * 4f);
            uv.Add(new Vector2(0f, v0)); uv.Add(new Vector2(u1, v0)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2(0f, v1));
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            else t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        public void Box(Vector3 c, Vector3 size, int band, Quaternion? rotation = null)
        {
            var rot = rotation ?? Quaternion.identity;
            Vector3 x = rot * Vector3.right * size.x / 2f, y = rot * Vector3.up * size.y / 2f, z = rot * Vector3.forward * size.z / 2f;
            float len = Mathf.Max(size.x, size.y, size.z);
            Quad(c - x - y + z, c + x - y + z, c + x + y + z, c - x + y + z, z.normalized, band, len);
            Quad(c + x - y - z, c - x - y - z, c - x + y - z, c + x + y - z, -z.normalized, band, len);
            Quad(c + x - y + z, c + x - y - z, c + x + y - z, c + x + y + z, x.normalized, band, len);
            Quad(c - x - y - z, c - x - y + z, c - x + y + z, c - x + y - z, -x.normalized, band, len);
            Quad(c - x + y + z, c + x + y + z, c + x + y - z, c - x + y - z, y.normalized, band, len);
            Quad(c - x - y - z, c + x - y - z, c + x - y + z, c - x - y + z, -y.normalized, band, len);
        }

        // a tube along `axis` from a to b, 8 sides, capped
        public void Tube(Vector3 a, Vector3 b, float r, int band, int sides = 8)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized, w = Vector3.Cross(axis, u);
            float len = (b - a).magnitude;
            for (int k = 0; k < sides; k++)
            {
                float a0 = 2f * Mathf.PI * k / sides, a1 = 2f * Mathf.PI * (k + 1) / sides;
                Vector3 d0 = u * Mathf.Cos(a0) + w * Mathf.Sin(a0), d1 = u * Mathf.Cos(a1) + w * Mathf.Sin(a1);
                Quad(a + d0 * r, b + d0 * r, b + d1 * r, a + d1 * r, ((d0 + d1) / 2f).normalized, band, len);
                Tri(a, a + d1 * r, a + d0 * r, -axis, band);     // end caps
                Tri(b, b + d0 * r, b + d1 * r, axis, band);
            }
        }

        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, int band)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            for (int k = 0; k < 3; k++) { n.Add(normal); uv.Add(new Vector2(0.02f * k, band * 0.25f + 0.05f)); }
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) t.AddRange(new[] { i, i + 2, i + 1 });
            else t.AddRange(new[] { i, i + 1, i + 2 });
        }

        public Mesh ToMesh()
        {
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    static Mesh BuildGun()
    {
        var b = new Builder();
        const float bore = 0.127f, r = 0.0115f, front = -0.405f, breech = -0.035f;
        // two barrels side by side, a rib on top
        foreach (float x in new[] { -0.0118f, 0.0118f })
            b.Tube(new Vector3(x, bore, breech), new Vector3(x, bore, front), r, Metal);
        b.Box(new Vector3(0f, bore + 0.012f, (breech + front) / 2f), new Vector3(0.007f, 0.006f, breech - front), Metal);
        // receiver, top lever, hammers
        b.Box(new Vector3(0f, 0.122f, -0.008f), new Vector3(0.036f, 0.056f, 0.075f), Metal);
        b.Box(new Vector3(0f, 0.153f, 0.022f), new Vector3(0.010f, 0.008f, 0.03f), Metal);
        foreach (float x in new[] { -0.009f, 0.009f })
            b.Box(new Vector3(x, 0.152f, 0.03f), new Vector3(0.006f, 0.016f, 0.01f), Metal, Quaternion.Euler(-25f, 0f, 0f));
        // fore-end under the barrels
        b.Box(new Vector3(0f, 0.104f, -0.14f), new Vector3(0.03f, 0.022f, 0.15f), Wood);
        // pistol grip, raked back like the pistol's, and the wrist joining it to the receiver
        b.Box(new Vector3(0f, 0.058f, 0.052f), new Vector3(0.03f, 0.11f, 0.046f), Wood, Quaternion.Euler(-14f, 0f, 0f));
        b.Box(new Vector3(0f, 0.108f, 0.042f), new Vector3(0.032f, 0.036f, 0.05f), Wood);
        b.Box(new Vector3(0f, 0.003f, 0.06f), new Vector3(0.032f, 0.008f, 0.052f), Metal, Quaternion.Euler(-14f, 0f, 0f));   // grip cap
        // trigger guard and trigger
        b.Box(new Vector3(0f, 0.07f, -0.006f), new Vector3(0.008f, 0.006f, 0.05f), Metal);
        b.Box(new Vector3(0f, 0.082f, -0.031f), new Vector3(0.008f, 0.03f, 0.006f), Metal);
        b.Box(new Vector3(0f, 0.086f, -0.008f), new Vector3(0.005f, 0.024f, 0.006f), Metal, Quaternion.Euler(15f, 0f, 0f));
        return b.ToMesh();
    }

    static Mesh BuildShell()
    {
        // a 12-gauge shell along +z: red hull, brass head
        var b = new Builder();
        b.Tube(new Vector3(0f, 0f, -0.033f), new Vector3(0f, 0f, 0.018f), 0.0105f, Red);
        b.Tube(new Vector3(0f, 0f, 0.018f), new Vector3(0f, 0f, 0.034f), 0.0108f, Brass);
        b.Tube(new Vector3(0f, 0f, 0.033f), new Vector3(0f, 0f, 0.036f), 0.0122f, Brass);
        return b.ToMesh();
    }
}
