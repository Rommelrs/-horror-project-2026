#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only helpers for the checkpoint audit (called from editor test commands): a state snapshot of the player and the
/// world, the errors logged since the last snapshot, dying the real way and continuing like the Game Over menu does.
/// </summary>
public static class CheckpointAudit
{
    static readonly List<string> errors = new List<string>();
    static bool hooked;

    public static void Hook()
    {
        if (hooked) return;
        hooked = true;
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string first = (stack ?? "").Split('\n').FirstOrDefault(l => l.Contains("Assets/") || l.Contains("(at")) ?? "";
            errors.Add(msg.Split('\n')[0] + (first.Length > 0 ? "  <" + first.Trim() + ">" : ""));
        };
    }

    public static string CheckpointFile => Path.Combine(Application.persistentDataPath, "Saves", "checkpoint.json");

    public static CheckpointData ReadCheckpoint()
    {
        if (!File.Exists(CheckpointFile)) return null;
        return JsonUtility.FromJson<CheckpointData>(File.ReadAllText(CheckpointFile));
    }

    public static string CheckpointSummary()
    {
        var d = ReadCheckpoint();
        if (d == null) return "checkpoint.json: none";
        var flags = d.usedInteractables.Where(s => s.StartsWith("flag:")).Select(s => s.Substring(5));
        return $"checkpoint.json: '{d.checkpointName}' scene {d.sceneName} at ({d.posX:F1}, {d.posY:F1}, {d.posZ:F1}) hp {d.health} weapon {d.weaponEnabled} ammo {d.currentAmmo}"
             + $"\n   items: {string.Join(", ", d.items.Select(i => i.itemName + "x" + i.quantity))}"
             + $"\n   flags: {string.Join(", ", flags)}"
             + $"\n   dead {d.deadEnemies.Count}, picked {d.pickedUpItems.Count}, used {d.usedInteractables.Count}, zones {d.triggeredZones.Count}, switches {d.activatedSwitches.Count}, music '{d.currentMusicTrack}'";
    }

    /// <summary>True once a (re)loaded scene has its player back with the checkpoint inventory in.</summary>
    public static bool Ready => Player.instance != null && !CheckpointManager.IsLoadingCheckpoint && CheckpointManager.pendingRestoreData == null
                                && !LoadingHandler.IsLoading();

    public static string Snapshot(string label)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- {label}  [{SceneManager.GetActiveScene().name}] t={Time.time:F1} timeScale {Time.timeScale} paused {GameManager.IsPaused} gameOver {(LevelManager.instance != null && LevelManager.instance.isGameOver)}");
        var p = Player.instance;
        if (p == null) { sb.AppendLine("   NO PLAYER"); }
        else
        {
            Vector3 pos = p.transform.position;
            string ground = "NO GROUND";
            var hits = Physics.RaycastAll(pos + Vector3.up * 1.0f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore)
                              .Where(h => !h.collider.transform.IsChildOf(p.transform)).OrderBy(h => h.distance).ToArray();
            if (hits.Length > 0) ground = $"ground {hits[0].collider.name} {(pos.y - hits[0].point.y):F2} below feet";
            var cc = p.controller;
            string stuck = "";
            if (cc != null)
            {
                Vector3 c = p.transform.TransformPoint(cc.center);
                float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
                var over = Physics.OverlapCapsule(c + Vector3.up * half, c - Vector3.up * (half - 0.05f), cc.radius * 0.9f, ~0, QueryTriggerInteraction.Ignore)
                                  .Where(o => !o.transform.IsChildOf(p.transform)).Select(o => o.name).ToArray();
                if (over.Length > 0) stuck = "  OVERLAPS: " + string.Join(", ", over);
            }
            sb.AppendLine($"   player ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) yaw {p.transform.eulerAngles.y:F0}  {ground}{stuck}");
            var mv = p.GetComponent<PlayerMovement>();
            var pi = p.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            sb.AppendLine($"   hp {(p.health != null ? p.health.GetHealthValue() : -1)}  stab {(p.playerStability != null ? p.playerStability.stability : -1)}  weaponEnabled {(p.playerWeaponSystem != null && p.playerWeaponSystem.weaponIsEnabled)} ammo {(p.playerWeaponSystem != null ? p.playerWeaponSystem.currentAmmo : -1)}  move {(mv != null && mv.enabled)} input {(pi != null && pi.enabled)} cc {(cc != null && cc.enabled)} hasMap {p.hasMap}");
            if (p.inventory != null)
                sb.AppendLine($"   inventory: {string.Join(", ", p.inventory.GetItems().Select(s => s.item.itemName + "x" + s.quantity))} | notes {p.inventory.GetNotes().Count}");
        }
        if (SaveManager.instance != null)
        {
            var used = SaveManager.instance.GetUsedInteractables();
            sb.AppendLine($"   flags: {string.Join(", ", used.Where(s => s.StartsWith("flag:")).Select(s => s.Substring(5)))}");
            sb.AppendLine($"   dead {SaveManager.instance.GetDeadEnemies().Count}, picked {SaveManager.instance.GetPickedUpItems().Count}, used {used.Count}, zones {SaveManager.instance.GetTriggeredZones().Count}, switches {SaveManager.instance.GetActivatedSwitches().Count}, music '{SaveManager.instance.GetCurrentMusicTrack()}'");
        }
        var playing = Object.FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None).Where(d => d.state == PlayState.Playing).Select(d => d.name + "@" + d.time.ToString("F1"));
        string pl = string.Join(", ", playing);
        if (pl.Length > 0) sb.AppendLine("   PLAYING: " + pl);
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) sb.AppendLine("   subtitle busy");
        sb.AppendLine("   " + CheckpointSummary().Replace("\n", "\n   "));
        if (errors.Count > 0)
        {
            sb.AppendLine($"   ERRORS ({errors.Count}):");
            foreach (var e in errors.Distinct().Take(12)) sb.AppendLine("     " + e);
        }
        errors.Clear();
        return sb.ToString();
    }

    public static void ClearErrors() => errors.Clear();

    /// <summary>Dies the real way (Health 0 -> LevelManager game over after 4 s).</summary>
    public static void Kill()
    {
        var p = Player.instance;
        if (p == null || p.health == null) return;
        p.health.Damage(100000, true);
    }

    /// <summary>What the Game Over menu's Continue does.</summary>
    public static void Continue()
    {
        if (CheckpointManager.instance != null) CheckpointManager.instance.ContinueFromCheckpoint();
    }

    /// <summary>Quit-to-desktop-and-Continue: forget every in-memory copy so only checkpoint.json counts.</summary>
    public static void ColdContinue()
    {
        StoryFlags.ResetSession();
        CutsceneTrigger.ResetPlayed();
        Continue();
    }
}
#endif
