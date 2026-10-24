using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;

// Debug: logs, every frame for `seconds`, where a head bone points relative to a body's facing (pitch up/down and turn,
// degrees) next to the playing timeline's time, and writes it to `outputPath` as CSV. For hunting odd head moves.
public class CutsceneHeadLogger : MonoBehaviour
{
    public Transform head, body;
    [Tooltip("The head bone's local axis that points out of the face.")]
    public Vector3 faceAxis = Vector3.forward;
    public float seconds = 4f;
    public string outputPath;

    readonly StringBuilder csv = new StringBuilder("time,timeline,pitchUp,turn\n");
    float until;

    void Start() => until = Time.time + seconds;

    void LateUpdate()
    {
        if (head == null || body == null) return;
        PlayableDirector playing = null;
        foreach (var d in FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))
            if (d.state == PlayState.Playing) { playing = d; break; }
        Vector3 face = head.TransformDirection(faceAxis);
        float pitch = Mathf.Asin(Mathf.Clamp(face.y, -1f, 1f)) * Mathf.Rad2Deg;
        float turn = Vector3.SignedAngle(Vector3.ProjectOnPlane(body.forward, Vector3.up), Vector3.ProjectOnPlane(face, Vector3.up), Vector3.up);
        csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:F3},{1},{2:F1},{3:F1}\n",
            playing != null ? playing.time : -1, playing != null ? playing.name : "-", pitch, turn);
        if (Time.time > until)
        {
            File.WriteAllText(outputPath, csv.ToString());
            Destroy(this);
        }
    }
}
