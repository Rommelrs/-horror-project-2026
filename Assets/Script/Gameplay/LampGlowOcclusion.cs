using UnityEngine;

// The lamp glow (Custom/LampGlow) is slid inside the camera's short far clip so a far lamp still shows up as a distant light, but
// that also means a building or hill beyond the far clip is no longer drawn, so nothing is left in the depth buffer to hide the glow
// behind it. This checks line of sight with physics instead: if solid colliders sit between the camera and the lamp, the glow fades
// out (and back in when the view clears), whether or not the thing in the way is being rendered.
[RequireComponent(typeof(MeshRenderer))]
public class LampGlowOcclusion : MonoBehaviour
{
    [Tooltip("What can hide the lamp. Triggers are always ignored. Leave out the player, enemies and anything that isn't solid scenery.")]
    [SerializeField] LayerMask blockers = ~0;

    [Tooltip("The sight line stops this far (world metres) short of the lamp, so the wall or post it is mounted on doesn't hide it.")]
    [SerializeField] float endMargin = 0.5f;

    [Tooltip("Sideways spread of the extra sight lines (metres): lets the glow fade out gradually as an edge slides over it.")]
    [SerializeField] float spread = 0.3f;

    [Tooltip("Seconds to fade fully in or out.")]
    [SerializeField] float fadeTime = 0.2f;

    [Tooltip("Beyond this the glow is invisible anyway, so no check is done. Keep at or above the material's 'Visible up to'.")]
    [SerializeField] float maxCheckDistance = 60f;

    static readonly int VisibilityId = Shader.PropertyToID("_Visibility");

    MeshRenderer glowRenderer;
    MaterialPropertyBlock block;
    Camera cam;
    float visibility = 1f;
    bool initialised;

    void Awake()
    {
        glowRenderer = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
    }

    // for components that change how far the glow can be seen (e.g. VehicleHeadlights)
    public void SetMaxCheckDistance(float metres) { maxCheckDistance = metres; }

    void OnDisable()
    {
        if (glowRenderer == null) return;
        // back to fully visible, keeping any other per-object glow settings (e.g. the fog distance set by VehicleHeadlights)
        if (block == null) block = new MaterialPropertyBlock();
        glowRenderer.GetPropertyBlock(block);
        block.SetFloat(VisibilityId, 1f);
        glowRenderer.SetPropertyBlock(block);
        initialised = false;
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        float target = Sample();
        visibility = !initialised || fadeTime <= 0f ? target : Mathf.MoveTowards(visibility, target, Time.deltaTime / fadeTime);
        initialised = true;

        glowRenderer.GetPropertyBlock(block);
        block.SetFloat(VisibilityId, visibility);
        glowRenderer.SetPropertyBlock(block);
    }

    // fraction (0..1) of the sight lines from the camera to the lamp that are clear
    float Sample()
    {
        Vector3 camPos = cam.transform.position;
        Vector3 lamp = transform.position;
        Vector3 toLamp = lamp - camPos;
        float dist = toLamp.magnitude;
        if (dist > maxCheckDistance) return 1f;       // too far to see anyway: the shader has faded it out
        if (dist <= endMargin) return 1f;

        Vector3 dir = toLamp / dist;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        if (side.sqrMagnitude < 1e-4f) side = cam.transform.right;
        side.Normalize();

        int clear = 0;
        for (int i = -1; i <= 1; i++)
        {
            Vector3 end = lamp + side * (spread * i) - dir * endMargin;
            if (!Physics.Linecast(camPos, end, blockers, QueryTriggerInteraction.Ignore)) clear++;
        }
        return clear / 3f;
    }
}
