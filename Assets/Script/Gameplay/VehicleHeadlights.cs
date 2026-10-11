using UnityEngine;

// Switches a vehicle's headlights (the spot lights, the fog-piercing glow billboards and the pool of light on the ground under this
// object) on and off, and sets how far away the glow can be seen through the fog.
// Hook SetOn up to a UnityEvent (a trigger, a cutscene signal, a power switch) to turn them on or off during play.
public class VehicleHeadlights : MonoBehaviour
{
    [SerializeField] bool startOn = true;

    [Header("Fog piercing")]
    [Tooltip("The glow starts to show through the fog from this far away (metres): a faint speck at the edge, brighter and bigger as the player gets closer. 0 = no glow through fog.")]
    [SerializeField, Min(0f)] float visibleFromDistance = 80f;

    [Tooltip("How the glow ramps up on approach. 1 = steadily brighter; higher = stays very faint until fairly close, then rises quickly.")]
    [SerializeField, Range(1f, 4f)] float approachCurve = 1.7f;

    [Header("Light in the air")]
    [Tooltip("The visible cones of light hanging in the fog in front of each lamp (one per headlight).")]
    [SerializeField] Transform[] airBeams;
    [Tooltip("How far in front of the lamps the cones reach (metres).")]
    [SerializeField, Min(0.5f)] float airBeamLength = 7f;
    [Tooltip("Width of each cone at its far end (metres).")]
    [SerializeField, Min(0.1f)] float airBeamWidth = 1.6f;
    [Tooltip("Height of each cone at its far end (metres). Flatter than it is wide, like a real low beam.")]
    [SerializeField, Min(0.1f)] float airBeamHeight = 1.0f;
    [Tooltip("How bright the cones are. 0 = hidden.")]
    [SerializeField, Range(0f, 2f)] float airBeamBrightness = 0.35f;

    [Header("Light on the ground")]
    [Tooltip("The two oval beams of light thrown on the ground in front of the vehicle, one per headlight.")]
    [SerializeField] Transform lightPool;
    [Tooltip("Width of the area the beams are drawn in (metres). Make it wider than both beams side by side.")]
    [SerializeField, Min(0.1f)] float poolWidth = 3.2f;
    [Tooltip("How far in front of the headlights the beams reach (metres).")]
    [SerializeField, Min(0.1f)] float poolLength = 5.5f;
    [Tooltip("Width of each beam at its widest (metres).")]
    [SerializeField, Min(0.1f)] float beamWidth = 1.1f;
    [Tooltip("How bright the pool is. 0 = hidden.")]
    [SerializeField, Range(0f, 2f)] float poolBrightness = 0.8f;
    [Tooltip("Sits this far above the collider ground (metres). Ground that is a mesh without a collider (a paved slab laid over the terrain) can sit higher than the collider under it: if the pool is hidden, raise this; if it floats, lower it.")]
    [SerializeField] float groundLift = 0.12f;

    static readonly int MaxDistId = Shader.PropertyToID("_MaxDist");
    static readonly int CurveId = Shader.PropertyToID("_Curve");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int GroundYId = Shader.PropertyToID("_GroundY");
    static readonly int HalfSpacingId = Shader.PropertyToID("_HalfSpacing");
    static readonly int BeamHalfWidthId = Shader.PropertyToID("_BeamHalfWidth");

    // ground height under the beams (set by SnapPoolToGround), so the air beams can fade out as they come down to it
    [SerializeField, HideInInspector] float groundHeight = -1000f;

    Light[] lights;
    MeshRenderer[] glows;

    void Awake()
    {
        lights = GetComponentsInChildren<Light>(true);
        glows = GetComponentsInChildren<MeshRenderer>(true);
        ApplyFogDistance();
        ApplyPool();
        ApplyAirBeams();
    }

    void Start()
    {
        SnapPoolToGround();
        SetOn(startOn);
    }

    // changing the numbers in the Inspector updates the glow straight away, in the editor too
    void OnValidate()
    {
        ApplyFogDistance();
        ApplyPool();
        ApplyAirBeams();
    }

    public void SetOn(bool on)
    {
        foreach (Light l in lights) l.enabled = on;
        foreach (MeshRenderer g in glows) g.enabled = on;
    }

    public void TurnOn() { SetOn(true); }
    public void TurnOff() { SetOn(false); }

    // set from code (e.g. a scripted event: "the lights reach out further")
    public void SetVisibleFromDistance(float metres)
    {
        visibleFromDistance = Mathf.Max(0f, metres);
        ApplyFogDistance();
    }

    void ApplyFogDistance()
    {
        // per-object override of the shared glow materials, so each vehicle can have its own distance
        var block = new MaterialPropertyBlock();
        foreach (MeshRenderer g in GetComponentsInChildren<MeshRenderer>(true))
        {
            g.GetPropertyBlock(block);
            block.SetFloat(MaxDistId, Mathf.Max(visibleFromDistance, 0.01f));
            block.SetFloat(CurveId, approachCurve);
            g.SetPropertyBlock(block);

            LampGlowOcclusion occlusion = g.GetComponent<LampGlowOcclusion>();
            if (occlusion != null) occlusion.SetMaxCheckDistance(visibleFromDistance + 10f);
        }
    }

    void ApplyAirBeams()
    {
        if (airBeams == null) return;
        var block = new MaterialPropertyBlock();
        foreach (Transform t in airBeams)
        {
            if (t == null) continue;
            t.localScale = new Vector3(airBeamWidth * 0.5f, airBeamHeight * 0.5f, airBeamLength);
            MeshRenderer mr = t.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            mr.GetPropertyBlock(block);
            block.SetFloat(IntensityId, airBeamBrightness);
            // the beams fade out as they come down to the ground
            block.SetFloat(GroundYId, groundHeight);
            mr.SetPropertyBlock(block);
        }
    }

    void ApplyPool()
    {
        if (lightPool == null) return;
        lightPool.localScale = new Vector3(poolWidth, 1f, poolLength);

        // each beam starts at its lamp: half the distance between the two spot lights, measured across the vehicle
        float halfSpacing = 0.63f;
        Light[] spots = GetComponentsInChildren<Light>(true);
        if (spots.Length >= 2)
            halfSpacing = Mathf.Abs(transform.InverseTransformPoint(spots[0].transform.position).x - transform.InverseTransformPoint(spots[1].transform.position).x) * 0.5f;

        MeshRenderer mr = lightPool.GetComponent<MeshRenderer>();
        if (mr == null) return;
        var block = new MaterialPropertyBlock();
        mr.GetPropertyBlock(block);
        block.SetFloat(IntensityId, poolBrightness);
        block.SetFloat(HalfSpacingId, halfSpacing);
        block.SetFloat(BeamHalfWidthId, beamWidth * 0.5f);
        mr.SetPropertyBlock(block);
    }

    // Finds the ground in front of the vehicle: the air beams fade out toward it, and the ground pool (if there is one) is laid on it.
    // In the editor use the component's context menu.
    [ContextMenu("Snap Light Pool To Ground")]
    public void SnapPoolToGround()
    {
        // look for the ground just ahead of the bumper, from above, so the vehicle's own colliders aren't hit
        Vector3 from = (lightPool != null ? lightPool.position : transform.position) + transform.forward * 1.0f + Vector3.up * 1.5f;
        int mask = ~LayerMask.GetMask("TransparentFX", "Ignore Raycast", "Water", "UI", "Player", "Enemy", "Hitbox", "Inspection", "Projectile");
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 6f, mask, QueryTriggerInteraction.Ignore))
        {
            groundHeight = hit.point.y + groundLift;       // the visible ground can sit a little above the collider
            if (lightPool != null)
            {
                Vector3 p = lightPool.position;
                p.y = hit.point.y + groundLift;
                lightPool.position = p;
            }
            ApplyAirBeams();
        }
    }
}
