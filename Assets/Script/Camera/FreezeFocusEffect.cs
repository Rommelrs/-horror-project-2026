using UnityEngine;

/// <summary>Fades the world out round a point while time stands still (Hidden/FreezeFocus). Added to the main camera by
/// GrabCounterTutorial; a plain blit while Amount is 0.</summary>
[RequireComponent(typeof(Camera))]
public class FreezeFocusEffect : MonoBehaviour
{
    public float Amount;
    public Vector3 focus;
    public float near = 1.5f, far = 3.6f;
    Material mat;
    Camera cam;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        cam.depthTextureMode |= DepthTextureMode.Depth;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (Amount <= 0.001f) { Graphics.Blit(src, dst); return; }
        if (mat == null)
        {
            var sh = Resources.Load<Shader>("Shaders/FreezeFocus");
            if (sh == null) { Graphics.Blit(src, dst); return; }
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }
        mat.SetMatrix("_InvVP", (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse);
        mat.SetVector("_Focus", focus);
        mat.SetFloat("_Amount", Amount);
        mat.SetFloat("_Near", near);
        mat.SetFloat("_Far", far);
        Graphics.Blit(src, dst, mat);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}
