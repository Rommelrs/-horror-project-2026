using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace RoadTool
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Road Tool/Road")]
    public class Road : MonoBehaviour
    {
        [Tooltip("SplineContainer to drive the road shape. If left null, the component will try GetComponent on this GameObject.")]
        public SplineContainer splineContainer;

        [Header("Width")]
        [Min(0.1f)] public float defaultWidth = 6f;
        [Tooltip("Per-knot width override. Length should equal the spline's knot count. Use the 'Sync Knot Widths' button in the inspector to resize.")]
        public List<float> knotWidths = new List<float>();

        [Header("Tessellation")]
        [Min(0.1f)] public float segmentsPerMeter = 2f;
        [Tooltip("How often to repeat the surface texture along the road length (in meters per UV tile).")]
        [Min(0.1f)] public float textureLengthScale = 4f;

        [Header("Lanes")]
        [Min(1)] public int laneCount = 2;
        public bool drawLaneMarkings = true;
        [Min(0.01f)] public float laneMarkingWidth = 0.15f;
        [Min(0.001f)] public float laneMarkingHeight = 0.02f;

        [Header("Materials")]
        public Material surfaceMaterial;
        public Material laneMarkingMaterial;

        Mesh _mesh;

        void OnEnable()
        {
            if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
            Rebuild();
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) Rebuild(); };
#endif
        }

        public float GetWidthAt(float t)
        {
            if (knotWidths == null || knotWidths.Count == 0) return defaultWidth;
            if (knotWidths.Count == 1) return knotWidths[0];
            float scaled = Mathf.Clamp01(t) * (knotWidths.Count - 1);
            int i0 = Mathf.FloorToInt(scaled);
            int i1 = Mathf.Min(i0 + 1, knotWidths.Count - 1);
            float f = scaled - i0;
            return Mathf.Lerp(knotWidths[i0], knotWidths[i1], f);
        }

        public void Rebuild()
        {
            var mesh = GetOrCreateMesh();
            if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count < 2)
            {
                mesh.Clear();
                return;
            }

            var spline = splineContainer.Spline;
            float length = SplineUtility.CalculateLength(spline, float4x4.identity);
            if (length <= 0.0001f) { mesh.Clear(); return; }

            int steps = Mathf.Max(2, Mathf.CeilToInt(length * segmentsPerMeter));

            var verts = new List<Vector3>(steps * 2);
            var uvs = new List<Vector2>(steps * 2);
            var normals = new List<Vector3>(steps * 2);
            var surfaceTris = new List<int>((steps - 1) * 6);

            // Surface ribbon
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                SplineUtility.Evaluate(spline, t, out float3 pos, out float3 tan, out float3 up);
                Vector3 P = (Vector3)pos;
                Vector3 T = math.lengthsq(tan) > 1e-8f ? ((Vector3)math.normalize(tan)) : Vector3.forward;
                Vector3 U = math.lengthsq(up) > 1e-8f ? ((Vector3)math.normalize(up)) : Vector3.up;
                Vector3 R = Vector3.Cross(U, T).normalized;
                float w = GetWidthAt(t) * 0.5f;

                verts.Add(P - R * w);
                verts.Add(P + R * w);
                float v = (t * length) / Mathf.Max(0.0001f, textureLengthScale);
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));
                normals.Add(U);
                normals.Add(U);
            }
            for (int i = 0; i < steps - 1; i++)
            {
                int b = i * 2;
                surfaceTris.Add(b);
                surfaceTris.Add(b + 2);
                surfaceTris.Add(b + 1);
                surfaceTris.Add(b + 1);
                surfaceTris.Add(b + 2);
                surfaceTris.Add(b + 3);
            }

            // Lane markings as second submesh
            var markingTris = new List<int>();
            if (drawLaneMarkings)
            {
                // Lateral offsets in [-1..1] (fraction of half-width): outer edges + lane dividers
                var offsets = new List<float> { -1f, 1f };
                for (int i = 1; i < laneCount; i++)
                    offsets.Add(-1f + 2f * i / laneCount);

                foreach (var off in offsets)
                {
                    int baseIdx = verts.Count;
                    for (int i = 0; i < steps; i++)
                    {
                        float t = i / (float)(steps - 1);
                        SplineUtility.Evaluate(spline, t, out float3 pos, out float3 tan, out float3 up);
                        Vector3 P = (Vector3)pos;
                        Vector3 T = math.lengthsq(tan) > 1e-8f ? ((Vector3)math.normalize(tan)) : Vector3.forward;
                        Vector3 U = math.lengthsq(up) > 1e-8f ? ((Vector3)math.normalize(up)) : Vector3.up;
                        Vector3 R = Vector3.Cross(U, T).normalized;
                        float w = GetWidthAt(t) * 0.5f;

                        Vector3 center = P + R * (off * w) + U * laneMarkingHeight;
                        Vector3 mLeft = center - R * (laneMarkingWidth * 0.5f);
                        Vector3 mRight = center + R * (laneMarkingWidth * 0.5f);
                        verts.Add(mLeft);
                        verts.Add(mRight);
                        float v = (t * length) / Mathf.Max(0.0001f, textureLengthScale);
                        uvs.Add(new Vector2(0f, v));
                        uvs.Add(new Vector2(1f, v));
                        normals.Add(U);
                        normals.Add(U);
                    }
                    for (int i = 0; i < steps - 1; i++)
                    {
                        int b = baseIdx + i * 2;
                        markingTris.Add(b);
                        markingTris.Add(b + 2);
                        markingTris.Add(b + 1);
                        markingTris.Add(b + 1);
                        markingTris.Add(b + 2);
                        markingTris.Add(b + 3);
                    }
                }
            }

            bool hasMarkings = drawLaneMarkings && markingTris.Count > 0;
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.subMeshCount = hasMarkings ? 2 : 1;
            mesh.SetTriangles(surfaceTris, 0);
            if (hasMarkings) mesh.SetTriangles(markingTris, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            var mr = GetComponent<MeshRenderer>();
            if (hasMarkings)
                mr.sharedMaterials = new[] { surfaceMaterial, laneMarkingMaterial };
            else
                mr.sharedMaterials = new[] { surfaceMaterial };
        }

        Mesh GetOrCreateMesh()
        {
            var mf = GetComponent<MeshFilter>();
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Road Mesh" };
                _mesh.hideFlags = HideFlags.DontSave;
            }
            mf.sharedMesh = _mesh;
            return _mesh;
        }
    }
}
