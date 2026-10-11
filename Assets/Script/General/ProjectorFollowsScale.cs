using UnityEngine;

// A Projector ignores its transform's scale (its size and clip planes are always in world units). This keeps them in the
// object's own (local) units instead, so a projected decal that is part of a prefab stays lined up with the prefab when an
// instance is scaled - e.g. the wet footprint trail on the park toilet, whose scene instance is scaled 1.34.
[ExecuteAlways]
[RequireComponent(typeof(Projector))]
public class ProjectorFollowsScale : MonoBehaviour
{
    [Tooltip("Half size of the projected area in this object's local units.")]
    [SerializeField] float localOrthographicSize = 2f;
    [SerializeField] float localNearClip = 0.02f;
    [SerializeField] float localFarClip = 1f;

    Projector projector;
    float lastScale = -1f;

    void OnEnable() { Apply(); }
    void OnValidate() { lastScale = -1f; Apply(); }

    void Update()
    {
        if (!Mathf.Approximately(transform.lossyScale.x, lastScale)) Apply();
    }

    void Apply()
    {
        if (projector == null) projector = GetComponent<Projector>();
        if (projector == null) return;
        float s = Mathf.Abs(transform.lossyScale.x);
        projector.orthographicSize = localOrthographicSize * s;
        projector.nearClipPlane = localNearClip * s;
        projector.farClipPlane = localFarClip * s;
        lastScale = transform.lossyScale.x;
    }
}
