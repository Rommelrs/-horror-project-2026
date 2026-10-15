using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds (or rebuilds) the QuestionerTestBench in the open scene: six enclosed test rooms placed away from the rest of the
/// Gym (own floor, own NavMesh surface), the spawn points, and the scenario list wired into the QuestionerTestBench component.
/// Re-running it deletes the old "QuestionerTestBench" root and generates it again.
/// </summary>
public static class QuestionerTestBenchBuilder
{
    const string RootName = "QuestionerTestBench";
    const string PrefabPath = "Assets/Prefab/Enemy/QuestionerEnemy.prefab";
    const string PrefabNoRunPath = "Assets/Prefab/Enemy/QuestionerEnemyNoRunning.prefab";
    const string BagBearerPath = "Assets/Prefab/Enemy/BagBearerEnemy.prefab";
    const string PistolPath ="Assets/Resources/Items/Pistol.asset";
    const string AmmoPath = "Assets/Resources/Items/Supplies/Light Ammo.asset";
    const string MatFolder = "Assets/Materials/QuestionerBench";

    static readonly Vector3 BenchOrigin = new Vector3(-100f, 0f, 120f);
    const float RoomSpacing = 34f;
    const float Half = 12f;       // interior half size
    const float WallH = 5f;
    const float WallT = 0.5f;

    static Material floorMat, wallMat, obstacleMat, platformMat;

    [MenuItem("Tools/Questioner Test Bench/Rebuild Bench In Open Scene")]
    public static void RebuildMenu()
    {
        Build(true);
    }

    public static QuestionerTestBench Build(bool bake)
    {
        var existing = GameObject.Find(RootName);
        if (existing != null)
        {
            // Remove the old baked NavMesh asset too, otherwise every rebuild leaves an orphan "NavMesh-QuestionerTestBench 1.asset" behind.
            var oldSurface = existing.GetComponent<NavMeshSurface>();
            if (oldSurface != null && oldSurface.navMeshData != null)
            {
                string oldPath = AssetDatabase.GetAssetPath(oldSurface.navMeshData);
                if (!string.IsNullOrEmpty(oldPath)) AssetDatabase.DeleteAsset(oldPath);
            }
            Object.DestroyImmediate(existing);
        }

        EnsureMaterials();

        var root = new GameObject(RootName);
        root.transform.position = BenchOrigin;
        var bench = root.AddComponent<QuestionerTestBench>();
        bench.questionerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        bench.questionerNoRunningPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabNoRunPath);
        bench.bagBearerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BagBearerPath);
        currentKind = QuestionerTestBench.EnemyKind.Questioner;
        bench.pistolItem = AssetDatabase.LoadAssetAtPath<Item>(PistolPath);
        bench.ammoItem = AssetDatabase.LoadAssetAtPath<Item>(AmmoPath);
        bench.scenarios = new List<QuestionerTestBench.Scenario>();

        // Geometry lives under its own child so the bench NavMeshSurface only bakes this.
        var geo = new GameObject("Geometry").transform;
        geo.SetParent(root.transform, false);
        var spawns = new GameObject("Spawns").transform;
        spawns.SetParent(root.transform, false);

        // Room centers (local): 3 columns x 2 rows
        Vector3 Room(int col, int row) { return new Vector3(col * RoomSpacing, 0f, row * RoomSpacing); }

        Transform A = MakeRoom(geo, spawns, "A Open Field", Room(0, 0));
        Transform B = MakeRoom(geo, spawns, "B Table", Room(1, 0));
        Transform C = MakeRoom(geo, spawns, "C Corridor", Room(2, 0));
        Transform D = MakeRoom(geo, spawns, "D Wall With Doorway", Room(0, 1));
        Transform E = MakeRoom(geo, spawns, "E Zigzag", Room(1, 1));
        Transform F = MakeRoom(geo, spawns, "F Platforms", Room(2, 1));

        // ---- room contents
        // A: two hiding walls
        Box(A, "HideWall L", new Vector3(-6f, 1.3f, 2f), new Vector3(4f, 2.6f, 0.6f), obstacleMat);
        Box(A, "HideWall R", new Vector3(6f, 1.3f, -2f), new Vector3(4f, 2.6f, 0.6f), obstacleMat);
        // B: big table block
        Box(B, "Table", new Vector3(0f, 1.2f, 0f), new Vector3(3f, 2.4f, 9f), obstacleMat);
        // C: corridor walls
        Box(C, "Corridor Wall L", new Vector3(-1.6f, WallH * 0.5f, 0f), new Vector3(0.4f, WallH, 16f), wallMat);
        Box(C, "Corridor Wall R", new Vector3(1.6f, WallH * 0.5f, 0f), new Vector3(0.4f, WallH, 16f), wallMat);
        // D: dividing wall with a 1.6m doorway at x = 6
        float gapL = 5.2f, gapR = 6.8f;
        Box(D, "Divider Left", new Vector3((-Half - 0.25f + gapL) * 0.5f, WallH * 0.5f, 0f), new Vector3(gapL + Half + 0.25f, WallH, WallT), wallMat);
        Box(D, "Divider Right", new Vector3((gapR + Half + 0.25f) * 0.5f, WallH * 0.5f, 0f), new Vector3(Half + 0.25f - gapR, WallH, WallT), wallMat);
        // E: three baffles forming an S-shaped path
        Box(E, "Baffle 1", new Vector3((-Half - 0.25f + 5f) * 0.5f, WallH * 0.5f, -6f), new Vector3(5f + Half + 0.25f, WallH, WallT), wallMat);
        Box(E, "Baffle 2", new Vector3((-5f + Half + 0.25f) * 0.5f, WallH * 0.5f, 0f), new Vector3(Half + 0.25f + 5f, WallH, WallT), wallMat);
        Box(E, "Baffle 3", new Vector3((-Half - 0.25f + 5f) * 0.5f, WallH * 0.5f, 6f), new Vector3(5f + Half + 0.25f, WallH, WallT), wallMat);
        // F: platform with a ramp (left), sheer platform (right)
        Box(F, "Platform Ramp (top y=2)", new Vector3(-6f, 1f, 6f), new Vector3(8f, 2f, 8f), platformMat);
        {
            float rise = 2f, run = 6f;
            float len = Mathf.Sqrt(rise * rise + run * run);
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(-ang, 0f, 0f);
            Vector3 mid = new Vector3(-6f, rise * 0.5f, 2f - run * 0.5f);
            Vector3 c = mid - rot * Vector3.up * 0.2f;
            Box(F, "Ramp", c, new Vector3(3f, 0.4f, len), platformMat, rot);
        }
        Box(F, "Platform Sheer (top y=3)", new Vector3(6f, 1.5f, 6f), new Vector3(8f, 3f, 8f), platformMat);

        // ---- scenarios
        // 1..9 first (numpad), the rest via PgUp/PgDn
        AddScenario(bench, spawns, A, "Open field - far chase (19 m)",
            "Idle -> Chase delay before the first step (he stares ~2 s)? Does he sprint then slow down, or wander/orbit instead of closing in (random offset point around you)? Does he get the first hit?",
            new Vector3(0f, 0f, -10f), 0f, 100, false,
            new[] { Enemy(new Vector3(0f, 0f, 9f), 180f) });

        AddScenario(bench, spawns, A, "Open field - point blank (2.8 m)",
            "Attack timing at close range. Step sideways/back: does he follow, turn too slowly (turn rate while attacking is low), or hit you from outside his arc? Does he whiff and re-chase?",
            new Vector3(0f, 0f, -2f), 0f, 100, false,
            new[] { Enemy(new Vector3(0f, 0f, 0.8f), 180f) });

        AddScenario(bench, spawns, A, "Open field - ambush from behind",
            "He spawns 5 m behind you facing away. How quickly does he turn and commit? Can you outrun / kite him?",
            new Vector3(0f, 0f, -4.5f), 0f, 100, false,
            new[] { Enemy(new Vector3(0f, 0f, -9.5f), 180f) });

        AddScenario(bench, spawns, A, "Open field - two Questioners",
            "Do they block each other, stack on the same spot, take different routes, or attack together? Do the random offset points cause them to bunch up?",
            new Vector3(0f, 0f, -10f), 0f, 100, false,
            new[] { Enemy(new Vector3(-9f, 0f, 9f), 150f), Enemy(new Vector3(9f, 0f, 9f), 210f) });

        AddScenario(bench, spawns, A, "Open field - gun duel (20 m, gun + ammo)",
            "Shoot him while he closes in. Count shots to kill (250 hp). Does knockback + the 2 s re-stare make him stun-lockable? Weak-point behaviour? Does he sidestep at range?",
            new Vector3(0f, 0f, -10f), 0f, 100, true,
            new[] { Enemy(new Vector3(0f, 0f, 10f), 180f) });

        AddScenario(bench, spawns, B, "Table loop - run around the block",
            "Run circles around the 3x9 block. Does he commit to one side, reverse constantly, jitter, or cut corners? Does sidestep (agent off, physics) push him into the table?",
            new Vector3(0f, 0f, -8f), 0f, 100, false,
            new[] { Enemy(new Vector3(0f, 0f, 8f), 180f) });

        AddScenario(bench, spawns, C, "Corridor - chase through a 2.8 m hallway",
            "Sidestep inside a narrow corridor (agent is disabled, rigidbody pushes sideways): does he wedge into the walls, get stuck, or fall off the NavMesh afterwards?",
            new Vector3(0f, 0f, -7f), 0f, 100, false,
            new[] { Enemy(new Vector3(0f, 0f, 11f), 180f) });

        AddScenario(bench, spawns, D, "Wall with doorway - player on the other side",
            "Straight-line distance is 16 m but the walk is 28 m through the door. Does he path through the door, press against the wall, or pick offset targets on the wrong side?",
            new Vector3(-6f, 0f, 8f), 180f, 100, false,
            new[] { Enemy(new Vector3(-6f, 0f, -8f), 0f) });

        AddScenario(bench, spawns, E, "Zigzag baffles - long winding path",
            "Path following around corners. Does he cut corners, hesitate at each turn, or lose time re-targeting? Compare the 4-5 s start delay with the walking time.",
            new Vector3(-8f, 0f, 10f), 180f, 100, false,
            new[] { Enemy(new Vector3(8f, 0f, -10f), 0f) });

        AddScenario(bench, spawns, D, "Through the wall - 1.8 m apart, wall in between",
            "You are 1.8 m away but a wall is between you. He must NOT attack the wall: he should walk the 23 m way around, and only attack once he has a clear line to you.",
            new Vector3(-6f, 0f, 0.9f), 180f, 100, false,
            new[] { Enemy(new Vector3(-6f, 0f, -0.9f), 0f) });

        AddScenario(bench, spawns, F, "Ramp platform - you are on top (y=2)",
            "Player on a platform reachable only via the ramp. Does he find the ramp, or stand below / pace at the edge?",
            new Vector3(-6f, 2f, 7f), 180f, 100, true,
            new[] { Enemy(new Vector3(-6f, 0f, -9f), 0f) });

        AddScenario(bench, spawns, F, "Unreachable platform - you are on top (y=3)",
            "No route up. What does he do when the destination is unreachable: stand still, jitter, pace at the base, try to attack through the side? Shoot him from here.",
            new Vector3(6f, 3f, 7f), 180f, 100, true,
            new[] { Enemy(new Vector3(6f, 0f, -9f), 0f) });

        AddScenario(bench, spawns, A, "Sentinel patrol - sneak past (Wandering)",
            "He patrols 4 waypoints as Wandering (FOV 45 deg, 12 m). Sneak behind the hide walls. Does he notice you only inside the cone? How long does he take to give up (5 s out of view)?",
            new Vector3(0f, 0f, -11f), 0f, 100, false,
            new[] { EnemyPatrol(spawns, A, new Vector3(-9f, 0f, 8f), new[] { new Vector3(-9f, 0f, 8f), new Vector3(9f, 0f, 8f), new Vector3(9f, 0f, -6f), new Vector3(-9f, 0f, -6f) }) });

        AddScenario(bench, spawns, A, "Gunshot lure - he stands still facing away",
            "Wandering enemy standing 18 m away facing away. Fire the gun (20 m sound radius): does he go to Investigate, then notice you, or give up and go back?",
            new Vector3(0f, 0f, -10f), 0f, 100, true,
            new[] { EnemyWandering(new Vector3(0f, 0f, 8f), 0f) });

        AddScenario(bench, spawns, A, "Low stability (20) + gun - far chase",
            "Same as the far chase but with low stability: check vignette/audio distortion, reload speed and aiming while he approaches.",
            new Vector3(0f, 0f, -10f), 0f, 20, true,
            new[] { Enemy(new Vector3(0f, 0f, 9f), 180f) });

        AddScenario(bench, spawns, A, "Open field - pack of four",
            "Four Questioners start bunched together 19 m away. Do they walk straight at you (no circling)? Do some line up behind each other (queue) while others take their own route? Aim at them: do they sidestep sometimes?",
            new Vector3(0f, 0f, -10f), 0f, 100, true,
            new[] { Enemy(new Vector3(-1.5f, 0f, 9f), 180f), Enemy(new Vector3(1.5f, 0f, 9f), 180f), Enemy(new Vector3(-1.5f, 0f, 11.5f), 180f), Enemy(new Vector3(1.5f, 0f, 11.5f), 180f) });

        // ================= PAGE 2: BagBearer (thrower: attackRange 11, reloads with a weak point, lobs bags) =================
        currentKind = QuestionerTestBench.EnemyKind.BagBearer;

        AddScenario(bench, spawns, A, "Open field - far chase (19 m)",
            "Does he close to ~11 m, stop, reload (weak point shows), then throw? Time to first throw. Does he throw while you are moving, and do the bags land near you? Watch the HOLDING tag.",
            new Vector3(0f, 0f, -10f), 0f, 100, false,
            new[] { Enemy(new Vector3(0f, 0f, 9f), 180f) });

        AddScenario(bench, spawns, A, "Open field - point blank (3 m)",
            "A thrower with you in his face. Does he throw at your feet, back off, keep walking into you, or freeze? Is the 4 s attack cooldown a free pass?",
            new Vector3(0f, 0f, -2f), 0f, 100, false,
            new[] { Enemy(new Vector3(0f, 0f, 1f), 180f) });

        AddScenario(bench, spawns, A, "Open field - ambush from behind",
            "He spawns 5 m behind you facing away. How long until his first throw? Does he turn before throwing?",
            new Vector3(0f, 0f, -4.5f), 0f, 100, false,
            new[] { Enemy(new Vector3(0f, 0f, -9.5f), 180f) });

        AddScenario(bench, spawns, A, "Open field - three BagBearers",
            "Throw stagger + attack slot: do they throw one at a time or all together, stack on one spot, block each other? Do bags land on top of each other?",
            new Vector3(0f, 0f, -10f), 0f, 100, false,
            new[] { Enemy(new Vector3(-4f, 0f, 9f), 180f), Enemy(new Vector3(0f, 0f, 11f), 180f), Enemy(new Vector3(4f, 0f, 9f), 180f) });

        AddScenario(bench, spawns, A, "Hide behind the low wall",
            "You stand right behind a 2.6 m wall (wall between you). Does he throw into the wall, wait at range, or walk around to get a clear shot? Do bags land on your side?",
            new Vector3(-6f, 0f, -1f), 0f, 100, false,
            new[] { Enemy(new Vector3(-6f, 0f, 9f), 180f) });

        AddScenario(bench, spawns, B, "Table block between you",
            "A 3x9x2.4 m block sits between you. Does he stay behind it lobbing bags, walk around, or hug the table? Compare the bag landing distance with the Hide scenario.",
            new Vector3(0f, 0f, -8f), 0f, 100, false,
            new[] { Enemy(new Vector3(0f, 0f, 8f), 180f) });

        AddScenario(bench, spawns, C, "Corridor - long straight hallway",
            "Bags in a 2.8 m wide hallway: do they hit the walls near him (spawn points blocked)? Does he keep reposition-jittering? Can you dodge by sidestepping?",
            new Vector3(0f, 0f, -7f), 0f, 100, false,
            new[] { Enemy(new Vector3(0f, 0f, 11f), 180f) });

        AddScenario(bench, spawns, D, "Through the wall - 1.8 m apart",
            "Wall between you at 1.8 m. He must NOT throw into the wall at point blank; he should walk the 23 m way round. Does he keep holding at the wall?",
            new Vector3(-6f, 0f, 0.9f), 180f, 100, false,
            new[] { Enemy(new Vector3(-6f, 0f, -0.9f), 0f) });

        AddScenario(bench, spawns, D, "Wall with doorway - far side",
            "16 m straight-line but through a doorway. Does he find the door, or stand in range (attackRange 11) pressed on the wall without a shot?",
            new Vector3(-6f, 0f, 8f), 180f, 100, false,
            new[] { Enemy(new Vector3(-6f, 0f, -8f), 0f) });

        AddScenario(bench, spawns, E, "Zigzag baffles - winding path",
            "Long path: does he sprint/slow sensibly, stop at the first spot with range but no line of sight, or get stuck in a corner (wall avoidance)?",
            new Vector3(-8f, 0f, 10f), 180f, 100, false,
            new[] { Enemy(new Vector3(8f, 0f, -10f), 0f) });

        AddScenario(bench, spawns, F, "Ramp platform - you are on top (y=2)",
            "Throwing UP at an elevated target (arc angle). Does he throw from below, walk up the ramp, or both? Where do bags land?",
            new Vector3(-6f, 2f, 7f), 180f, 100, true,
            new[] { Enemy(new Vector3(-6f, 0f, -9f), 0f) });

        AddScenario(bench, spawns, F, "Unreachable platform - you are on top (y=3)",
            "No route up. Can he still throw from the base (range 11)? Or does he pace / stand still / throw into the platform wall?",
            new Vector3(6f, 3f, 7f), 180f, 100, true,
            new[] { Enemy(new Vector3(6f, 0f, -9f), 0f) });

        AddScenario(bench, spawns, A, "Mixed squad - 2 BagBearers + 1 Questioner",
            "Throwers keep their distance while the Questioner rushes in: does the Questioner's queue/attack ignore them? Do bags land on the Questioner? Do they block each other's line of sight?",
            new Vector3(0f, 0f, -10f), 0f, 100, true,
            new[] { Enemy(new Vector3(-3f, 0f, 9f), 180f), Enemy(new Vector3(3f, 0f, 9f), 180f), EnemyQuestioner(new Vector3(0f, 0f, 10.5f), 180f) });

        AddScenario(bench, spawns, A, "Gun duel - weak point while he reloads (gun + ammo)",
            "Shoot him while he closes in. The weak point only shows while he reloads. Weak-point hit = knocked down? Body shot while he is reloading / throwing = interrupts? Shots to kill (350 hp). Does a hit make him drop the bag he is carrying?",
            new Vector3(0f, 0f, -10f), 0f, 100, true,
            new[] { Enemy(new Vector3(0f, 0f, 10f), 180f) });

        AddScenario(bench, spawns, A, "Gunshot lure - he stands still facing away",
            "Wandering BagBearer 18 m away facing away. Fire the gun (20 m sound radius): does he investigate, notice you, then reload and throw? Or give up and go back?",
            new Vector3(0f, 0f, -10f), 0f, 100, true,
            new[] { EnemyWandering(new Vector3(0f, 0f, 8f), 0f) });

        // Corridor: the button sits flush on the corridor wall (the 1.8 m default would be inside the wall)
        foreach (var sc in bench.scenarios)
            if (sc.name.StartsWith("Corridor")) sc.buttonOffset = new Vector3(1.3f, 1.3f, 0f);

        // ---- NavMesh surface (children only, physics colliders)
        var surface = root.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.agentTypeID = 0;

        EditorUtility.SetDirty(bench);
        EditorSceneManager.MarkSceneDirty(root.scene);

        if (bake)
            NavMeshAssetManager.instance.StartBakingSurfaces(new Object[] { surface });

        return bench;
    }

    // --------------------------------------------------------------- helpers

    // Builds a scenario using the room's spawn container. 'room' is used to parent the spawn objects and to convert room-local
    // positions to bench-local positions (rooms are children of "Geometry" at their local center).
    static void AddScenario(QuestionerTestBench bench, Transform spawns, Transform room, string name, string watch,
        Vector3 playerLocal, float playerYaw, int stability, bool gun, EnemyDef[] enemyDefs)
    {
        var sc = new QuestionerTestBench.Scenario { name = name, kind = currentKind, watchFor = watch, startStability = stability, giveGun = gun };
        int idx = bench.scenarios.Count + 1;
        var container = new GameObject(idx.ToString("00") + " " + name).transform;
        container.SetParent(spawns, false);

        var pt = new GameObject("PlayerSpawn").transform;
        pt.SetParent(container, false);
        pt.position = room.position + playerLocal;
        pt.rotation = Quaternion.Euler(0f, playerYaw, 0f);
        sc.playerSpawn = pt;

        var list = new List<QuestionerTestBench.EnemySpawn>();
        int n = 0;
        foreach (var def in enemyDefs)
        {
            n++;
            var et = new GameObject("EnemySpawn " + n).transform;
            et.SetParent(container, false);
            et.position = room.position + def.localPos;
            et.rotation = Quaternion.Euler(0f, def.yaw, 0f);
            var es = new QuestionerTestBench.EnemySpawn { point = et, overrideType = def.overrideType, type = def.type, overrideKind = def.overrideKind, kind = def.kind };
            if (def.waypointLocals != null)
            {
                var wps = new List<Transform>();
                int w = 0;
                foreach (var wl in def.waypointLocals)
                {
                    w++;
                    var wp = new GameObject("Waypoint " + w).transform;
                    wp.SetParent(et, false);
                    wp.position = room.position + wl;
                    // face the next waypoint
                    Vector3 next = room.position + def.waypointLocals[w % def.waypointLocals.Length];
                    Vector3 dir = next - wp.position; dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f) wp.rotation = Quaternion.LookRotation(dir);
                    wps.Add(wp);
                }
                es.waypoints = wps.ToArray();
            }
            list.Add(es);
        }
        sc.enemies = list.ToArray();
        bench.scenarios.Add(sc);
    }

    class EnemyDef
    {
        public Vector3 localPos; public float yaw; public bool overrideType; public EnemyType type; public Vector3[] waypointLocals;
        public bool overrideKind; public QuestionerTestBench.EnemyKind kind;
    }

    // Page the scenarios being added belong to (the scenario's default enemy)
    static QuestionerTestBench.EnemyKind currentKind = QuestionerTestBench.EnemyKind.Questioner;

    static EnemyDef Enemy(Vector3 local, float yaw) { return new EnemyDef { localPos = local, yaw = yaw }; }
    static EnemyDef EnemyQuestioner(Vector3 local, float yaw) { return new EnemyDef { localPos = local, yaw = yaw, overrideKind = true, kind = QuestionerTestBench.EnemyKind.Questioner }; }
    static EnemyDef EnemyWandering(Vector3 local, float yaw) { return new EnemyDef { localPos = local, yaw = yaw, overrideType = true, type = EnemyType.Wandering }; }
    static EnemyDef EnemyPatrol(Transform spawns, Transform room, Vector3 local, Vector3[] waypoints)
    {
        return new EnemyDef { localPos = local, yaw = 90f, overrideType = true, type = EnemyType.Wandering, waypointLocals = waypoints };
    }

    static Transform MakeRoom(Transform geo, Transform spawns, string name, Vector3 center)
    {
        var room = new GameObject(name).transform;
        room.SetParent(geo, false);
        room.localPosition = center;

        float size = Half * 2f + WallT * 2f;
        Box(room, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size), floorMat);
        Box(room, "Wall N", new Vector3(0f, WallH * 0.5f, Half + WallT * 0.5f), new Vector3(size, WallH, WallT), wallMat);
        Box(room, "Wall S", new Vector3(0f, WallH * 0.5f, -Half - WallT * 0.5f), new Vector3(size, WallH, WallT), wallMat);
        Box(room, "Wall E", new Vector3(Half + WallT * 0.5f, WallH * 0.5f, 0f), new Vector3(WallT, WallH, size), wallMat);
        Box(room, "Wall W", new Vector3(-Half - WallT * 0.5f, WallH * 0.5f, 0f), new Vector3(WallT, WallH, size), wallMat);

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(room, false);
        lightGo.transform.localPosition = new Vector3(0f, 6.5f, 0f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 34f;
        l.intensity = 1.1f;
        l.shadows = LightShadows.None;
        return room;
    }

    static GameObject Box(Transform parent, string name, Vector3 localCenter, Vector3 size, Material mat, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void EnsureMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "QuestionerBench");
        floorMat = GetMat("BenchFloor", new Color(0.30f, 0.31f, 0.33f));
        wallMat = GetMat("BenchWall", new Color(0.52f, 0.52f, 0.55f));
        obstacleMat = GetMat("BenchObstacle", new Color(0.80f, 0.45f, 0.20f));
        platformMat = GetMat("BenchPlatform", new Color(0.25f, 0.45f, 0.75f));
    }

    static Material GetMat(string name, Color c)
    {
        string path = MatFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = c;
        EditorUtility.SetDirty(m);
        return m;
    }
}
