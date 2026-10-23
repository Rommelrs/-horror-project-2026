using Cinemachine;
using UnityEngine;

// Slow push-in / drift for a cutscene virtual camera: while the camera is live it eases from its placed pose by
// `move` (in its own local space) and narrows its field of view by `fovChange`, over `duration` seconds.
// Goes back to the start pose whenever the camera stops being live, so a timeline can reuse it.
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class CutsceneCameraDrift : MonoBehaviour
{
    public Vector3 move = new Vector3(0f, 0f, 0.2f);
    [Tooltip("Degrees added to the field of view over the drift (negative = push in).")]
    public float fovChange = 0f;
    public float duration = 5f;

    CinemachineVirtualCamera vcam;
    Vector3 startPos;
    float startFov;
    float t;
    bool wasLive;

    void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
        startPos = transform.localPosition;
        startFov = vcam.m_Lens.FieldOfView;
    }

    void LateUpdate()
    {
        bool live = CinemachineCore.Instance.IsLive(vcam);
        if (!live)
        {
            if (wasLive) { transform.localPosition = startPos; vcam.m_Lens.FieldOfView = startFov; }
            t = 0f; wasLive = false;
            return;
        }
        wasLive = true;
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
        u = u * u * (3f - 2f * u);
        transform.localPosition = startPos + transform.localRotation * move * u;
        vcam.m_Lens.FieldOfView = startFov + fovChange * u;
    }
}
