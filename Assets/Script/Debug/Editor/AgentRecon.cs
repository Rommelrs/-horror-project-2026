#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Automation (Claude): lists objects by name / component / place.</summary>
public static class AgentRecon
{
    public static void Names(params string[] parts)
    {
        var sb = new StringBuilder();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.gameObject.activeInHierarchy) continue;
            string n = t.name.ToLower();
            if (!parts.Any(p => n.Contains(p))) continue;
            sb.AppendLine(AgentTools.PathOf(t) + "  " + t.position.ToString("F1") + " [" + string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name)) + "]");
        }
        File.WriteAllText(AgentTools.Dir + "insp.txt", sb.ToString());
    }

    public static void Types(params string[] types)
    {
        var sb = new StringBuilder();
        foreach (var c in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c == null || !types.Contains(c.GetType().Name)) continue;
            sb.AppendLine((c.gameObject.activeInHierarchy ? "" : "(off) ") + c.GetType().Name + "  " + AgentTools.PathOf(c.transform) + "  " + c.transform.position.ToString("F1") + " yaw " + c.transform.eulerAngles.y.ToString("F0"));
        }
        File.WriteAllText(AgentTools.Dir + "insp.txt", sb.ToString());
    }

    /// <summary>shots.txt: name x y z  tx ty tz  fov</summary>
    public static void Shots()
    {
        foreach (var line in File.ReadAllLines(AgentTools.Dir + "shots.txt"))
        {
            var p = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 8) continue;
            float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
            if (p[0].StartsWith("top")) AgentTools.Shot(p[0] + ".png", new Vector3(F(1), F(2), F(3)), new Vector3(F(4), F(5), F(6)), 60f, 900, 900, 300f, true, F(7));
            else AgentTools.Shot(p[0] + ".png", new Vector3(F(1), F(2), F(3)), new Vector3(F(4), F(5), F(6)), F(7), 800, 500, 120f);
        }
    }

    /// <summary>One picture per door: every DoorInteractable (both sides) and every locked door.</summary>
    public static void Doors()
    {
        var sb = new StringBuilder(); int k = 0;
        foreach (var c in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID).Where(m => m is DoorInteractable || (m is AudioInteraction && m.transform.parent != null && AgentTools.PathOf(m.transform).Contains("LockedDoors"))).OrderBy(m => AgentTools.PathOf(m.transform)))
        {
            var p = AgentTools.PathOf(c.transform).Replace("------------", "");
            if (p.Contains("OldEnv")) continue;
            var spots = new System.Collections.Generic.List<Vector3>();
            foreach (var t in c.GetComponentsInChildren<DoorInteractableTrigger>(true)) spots.Add(t.transform.position);
            if (spots.Count == 0) spots.Add(c.transform.position);
            Vector3 doorAt = c.transform.position + Vector3.up * (c is DoorInteractable ? 1.2f : 0.2f);
            if (c is DoorInteractable) { var rs = c.GetComponentsInChildren<Renderer>().Where(r => r.bounds.size.y > 1.5f).ToArray(); if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); doorAt = b.center; } }
            for (int s = 0; s < spots.Count && s < 2; s++)
            {
                Vector3 dir;
                if (c is DoorInteractable && spots.Count > 0) { dir = spots[s] - doorAt; dir.y = 0f; if (dir.sqrMagnitude < 0.05f) dir = Vector3.forward; dir = Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? new Vector3(Mathf.Sign(dir.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(dir.z)); }
                else
                {
                    dir = Vector3.forward; float best = -1f;
                    foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    {
                        float m = Physics.Raycast(doorAt + d * 0.4f, d, out var h, 6f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 6f;
                        if (m > best) { best = m; dir = d; }
                    }
                }
                string name = "door_" + k.ToString("00") + (s == 0 ? "a" : "b");
                AgentTools.Shot(name + ".png", doorAt + dir * 4f + Vector3.up * 0.5f, doorAt, 55f, 480, 360, 80f);
                sb.AppendLine(name + " " + p + " " + c.transform.position.ToString("F1"));
            }
            k++;
        }
        File.WriteAllText(AgentTools.Dir + "doors_list.txt", sb.ToString());
    }

    public static void Near(Vector3 at, float radius, bool colliders)
    {
        var sb = new StringBuilder();
        if (colliders)
        {
            Physics.SyncTransforms();
            foreach (var c in Physics.OverlapSphere(at, radius, ~0, QueryTriggerInteraction.Collide))
                sb.AppendLine((c.isTrigger ? "TRIGGER " : "") + c.GetType().Name + " " + AgentTools.PathOf(c.transform) + " b " + c.bounds.center.ToString("F2") + " size " + c.bounds.size.ToString("F2") + " renderer " + (c.TryGetComponent<Renderer>(out var r) ? r.enabled.ToString() : "-") + " layer " + c.gameObject.layer);
        }
        else
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (r.bounds.SqrDistance(at) < radius * radius && r.bounds.size.magnitude < 60f)
                    sb.AppendLine(r.GetType().Name + " " + AgentTools.PathOf(r.transform) + " b " + r.bounds.center.ToString("F2") + " size " + r.bounds.size.ToString("F2") + " mat " + (r.sharedMaterial ? r.sharedMaterial.name : "-") + (r.enabled ? "" : " (off)"));
        File.WriteAllText(AgentTools.Dir + "insp.txt", sb.ToString());
    }
}
#endif
