using UnityEngine;

// Drives a "Custom/LightPoolDecal" mesh from a real spot Light, so the fake light pool on an unlit surface (the road) always matches the lamp:
// move / rotate the light, change its angle, range, colour or intensity and the pool follows, in the editor as well as in play mode.
// The decal mesh itself is a copy of the road triangles under the lamp, lifted a few centimetres.
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class LightPoolDecal : MonoBehaviour
{
    [Tooltip("The spot light this pool belongs to.")]
    [SerializeField] Light sourceLight;

    [Tooltip("Pool brightness per unit of the light's intensity (the shader adds colour x light intensity x this).")]
    [SerializeField] float strengthPerIntensity = 0.4f;

    [Tooltip("Optional: a mesh lying in the pool that is lit by the REAL light (a lit-shader object such as the wooden plank). The pool is cut out around its bounding box so it isn't lit twice.")]
    [SerializeField] MeshFilter excludeObject;

    [Tooltip("How many metres smaller than its bounding box the cut-out is (the pool fades out over the last few cm under the edge of the object).")]
    [SerializeField] float holeShrink = 0.1f;

    static readonly int LampPos = Shader.PropertyToID("_LampPos");
    static readonly int LampDir = Shader.PropertyToID("_LampDir");
    static readonly int TanHalfAngle = Shader.PropertyToID("_TanHalfAngle");
    static readonly int Range = Shader.PropertyToID("_Range");
    static readonly int Strength = Shader.PropertyToID("_Strength");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int HoleRow0 = Shader.PropertyToID("_HoleRow0");
    static readonly int HoleRow1 = Shader.PropertyToID("_HoleRow1");
    static readonly int HoleRow2 = Shader.PropertyToID("_HoleRow2");
    static readonly int HoleCenter = Shader.PropertyToID("_HoleCenter");
    static readonly int HoleExtents = Shader.PropertyToID("_HoleExtents");

    MeshRenderer rend;
    MaterialPropertyBlock block;

    void OnEnable() { Apply(); }

    void OnValidate() { Apply(); }

    // runs right before any camera (game or scene view) draws this mesh, so the pool is always current, even in the editor
    void OnWillRenderObject() { Apply(); }

    void OnDisable()
    {
        if (rend != null && block != null)
        {
            rend.SetPropertyBlock(null);
        }
    }

    void Apply()
    {
        if (sourceLight == null) return;
        if (rend == null) rend = GetComponent<MeshRenderer>();
        if (block == null) block = new MaterialPropertyBlock();

        bool lit = sourceLight.enabled && sourceLight.gameObject.activeInHierarchy;
        Transform t = sourceLight.transform;

        rend.GetPropertyBlock(block);
        block.SetVector(LampPos, t.position);
        block.SetVector(LampDir, t.forward);
        block.SetFloat(TanHalfAngle, Mathf.Tan(sourceLight.spotAngle * 0.5f * Mathf.Deg2Rad));
        block.SetFloat(Range, sourceLight.range);
        block.SetFloat(Strength, lit ? sourceLight.intensity * strengthPerIntensity : 0f);
        block.SetColor(ColorId, sourceLight.color);

        // cut-out box = the excluded mesh's local bounds, in its own space. A flat board is only cut along its footprint (its thinnest axis is ignored).
        if (excludeObject != null && excludeObject.sharedMesh != null && excludeObject.gameObject.activeInHierarchy)
        {
            // the box is expressed in the object's rotated axes but in real world metres (mesh space x lossy scale), so the soft edge is a few cm wide
            Matrix4x4 m = excludeObject.transform.worldToLocalMatrix;
            Bounds b = excludeObject.sharedMesh.bounds;
            Vector3 s = excludeObject.transform.lossyScale;
            Vector3 sAbs = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            Vector3 e = Vector3.Scale(b.extents, sAbs);
            int thin = 0;
            if (e.y < e[thin]) thin = 1;
            if (e.z < e[thin]) thin = 2;
            for (int a = 0; a < 3; a++)
                if (a != thin) e[a] = Mathf.Max(0.02f, e[a] - holeShrink);   // a little smaller than the box so no dark ring is left around the object
            e[thin] = 1000f;
            block.SetVector(HoleRow0, m.GetRow(0) * s.x);
            block.SetVector(HoleRow1, m.GetRow(1) * s.y);
            block.SetVector(HoleRow2, m.GetRow(2) * s.z);
            block.SetVector(HoleCenter, Vector3.Scale(b.center, s));
            block.SetVector(HoleExtents, new Vector4(e.x, e.y, e.z, 1f));
        }
        else
        {
            block.SetVector(HoleExtents, Vector4.zero);
        }

        rend.SetPropertyBlock(block);
    }
}
