using System.IO;
using System.Text;
using UnityEngine;

// Debug: per frame, how far the bones of `target` moved relative to its root since the last frame (max and mean, metres),
// with whether the coward meeting is playing and the Animator's state. A pose snap shows up as one frame far above its
// neighbours. Writes `outputPath` (CSV) when destroyed or on Save().
public class PoseJumpLogger : MonoBehaviour
{
    public Transform target;
    public string outputPath;

    Transform[] bones;
    Vector3[] last;
    readonly StringBuilder csv = new StringBuilder("frame,time,cutscene,maxMove,meanMove,maxBone,state,normTime\n");
    bool saved;

    void LateUpdate()
    {
        if (target == null) return;
        if (bones == null) { bones = target.GetComponentsInChildren<Transform>(); last = new Vector3[bones.Length]; }
        float max = 0f, sum = 0f; string maxBone = "";
        for (int i = 0; i < bones.Length; i++)
        {
            Vector3 p = target.InverseTransformPoint(bones[i].position);
            float d = Time.frameCount > 1 ? (p - last[i]).magnitude : 0f;
            if (d > max) { max = d; maxBone = bones[i].name; }
            sum += d; last[i] = p;
        }
        var anim = target.GetComponent<Animator>();
        var st = anim != null ? anim.GetCurrentAnimatorStateInfo(0) : default;
        csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0},{1:F3},{2},{3:F4},{4:F4},{5},{6},{7:F3}\n",
            Time.frameCount, Time.time, CowardMeetingCutscene.IsPlaying ? 1 : 0, max, sum / bones.Length, maxBone,
            anim != null ? st.shortNameHash : 0, st.normalizedTime);
    }

    public void Save()
    {
        if (saved || string.IsNullOrEmpty(outputPath)) return;
        saved = true;
        File.WriteAllText(outputPath, csv.ToString());
    }

    void OnDestroy() => Save();
}
