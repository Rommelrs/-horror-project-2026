using UnityEngine;

/// <summary>
/// The Questioner's scream on Khalid's screen (Hidden/QuestionerScreamEcho): added to the main camera by PlayerCloseCombat
/// the first time it's needed, does nothing (a plain blit) between screams. Trigger(seconds, strength) starts one: a quick
/// attack, holds at full for `hold` of the time (blurred, echoing), then fades out over the rest.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ScreamEchoEffect : MonoBehaviour
{
    public Shader shader;
    [Tooltip("How much of the last frame lingers at full strength.")]
    [Range(0f, 0.95f)] public float feedback = 0.65f;
    [Tooltip("Blur radius at full strength (fraction of the screen).")]
    [Range(0f, 0.05f)] public float blur = 0.024f;
    [Tooltip("Full strength for this part of the time, then it fades out over the rest.")]
    [Range(0f, 1f)] public float hold = 0.5f;

    Material mat;
    RenderTexture history;
    float start = -99f, length = 1f, strength;

    public float Amount
    {
        get
        {
            float t = Time.time - start;
            if (t < 0f || t > length) return 0f;
            float attack = Mathf.Clamp01(t / 0.06f);
            float decay = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(length * hold, length, t));
            return attack * decay * strength;
        }
    }

    public void Trigger(float seconds, float strength01)
    {
        // a second scream while the first still rings only tops it up
        if (Amount > 0f && strength01 <= strength) { length = Mathf.Max(length, Time.time - start + seconds); return; }
        start = Time.time;
        length = Mathf.Max(0.1f, seconds);
        strength = Mathf.Clamp01(strength01);
    }

    public void Stop()
    {
        start = -99f;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        float a = Amount;
        if (a <= 0.001f || shader == null)
        {
            Graphics.Blit(src, dst);
            if (history != null) { history.Release(); Destroy(history); history = null; }
            return;
        }
        if (mat == null) mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        bool fresh = history == null || history.width != src.width || history.height != src.height;
        if (fresh)
        {
            if (history != null) { history.Release(); Destroy(history); }
            history = new RenderTexture(src.width, src.height, 0, src.format) { name = "ScreamEchoHistory" };
            Graphics.Blit(src, history);
        }
        mat.SetFloat("_Amount", a);
        mat.SetFloat("_Feedback", feedback * a);
        mat.SetFloat("_Phase", Time.time - start);
        mat.SetFloat("_Blur", blur);
        mat.SetTexture("_History", history);
        var tmp = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
        Graphics.Blit(src, tmp, mat);
        Graphics.Blit(tmp, history);
        Graphics.Blit(tmp, dst);
        RenderTexture.ReleaseTemporary(tmp);
    }

    void OnDisable()
    {
        if (history != null) { history.Release(); Destroy(history); history = null; }
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
