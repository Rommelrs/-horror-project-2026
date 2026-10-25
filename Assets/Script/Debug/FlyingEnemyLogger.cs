using System.IO;
using System.Text;
using UnityEngine;

// Debug: per frame, where a FlyingEnemy is (and its height above / distance from the player), its mode, speed and how
// many weak points are out, to `outputPath` (CSV) when destroyed or on Save(). For checking that it flies smoothly.
public class FlyingEnemyLogger : MonoBehaviour
{
    public FlyingEnemy target;
    public string outputPath;

    readonly StringBuilder csv = new StringBuilder("time,dt,x,y,z,mode,speed,heightAbovePlayer,horizDistToPlayer,weakpoints,health\n");
    Vector3 last; bool hasLast, saved;

    void LateUpdate()
    {
        if (target == null || Time.deltaTime <= 0f) return;
        Vector3 p = target.transform.position;
        float speed = hasLast ? (p - last).magnitude / Time.deltaTime : 0f;
        last = p; hasLast = true;
        var player = Player.instance != null ? Player.instance.transform.position : Vector3.zero;
        Vector3 flat = p - player; flat.y = 0f;
        int weak = target.enemyWeakpoint != null ? target.enemyWeakpoint.GetSpawnedWeakpoints().Count : 0;
        csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:F3},{1:F4},{2:F3},{3:F3},{4:F3},{5},{6:F3},{7:F2},{8:F2},{9},{10}\n",
            Time.time, Time.deltaTime, p.x, p.y, p.z, target.currentState, speed, p.y - player.y, flat.magnitude, weak, target.health.GetHealthValue());
    }

    public void Save()
    {
        if (saved || string.IsNullOrEmpty(outputPath)) return;
        saved = true;
        File.WriteAllText(outputPath, csv.ToString());
    }

    void OnDestroy() => Save();
}
