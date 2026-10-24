using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;

// Debug: logs the main camera's position and facing every frame for `seconds`, next to the playing timeline's time
// (and optionally a target's position and facing), to `outputPath` as CSV. For seeing whether a shot change is a clean
// cut or a swoop.
public class CameraPathLogger : MonoBehaviour
{
    public float seconds = 3f;
    public string outputPath;
    [Tooltip("Optional: also log this transform (e.g. what the camera follows).")]
    public Transform target;

    readonly StringBuilder csv = new StringBuilder("frame,timeline,time,x,y,z,yaw,pitch,targetX,targetZ,targetYaw\n");
    float until;

    void Start() => until = Time.unscaledTime + seconds;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        PlayableDirector playing = null;
        foreach (var d in FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))
            if (d.state == PlayState.Playing) { playing = d; break; }
        Vector3 p = cam.transform.position, e = cam.transform.eulerAngles;
        Vector3 tp = target != null ? target.position : Vector3.zero;
        csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2:F3},{3:F2},{4:F2},{5:F2},{6:F1},{7:F1},{8:F2},{9:F2},{10:F1}\n",
            Time.frameCount, playing != null ? playing.name : "-", playing != null ? playing.time : -1, p.x, p.y, p.z, e.y, e.x,
            tp.x, tp.z, target != null ? target.eulerAngles.y : 0f);
        if (Time.unscaledTime > until)
        {
            File.WriteAllText(outputPath, csv.ToString());
            Destroy(this);
        }
    }
}
