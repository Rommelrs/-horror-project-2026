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

        [Header("Thickness (Y)")]
        [Tooltip("Vertical thickness extruded downward from the spline. Top surface stays at the spline; bottom sits below.")]
        [Min(0f)] public float thickness = 0f;

        [Header("Tessellation")]
        [Min(0.1f)] public float segmentsPerMeter = 2f;
        [Tooltip("How often to repeat the surface texture along the road length (in meters per UV tile).")]
        [Min(0.1f)] public float textureLengthScale = 4f;

        [Header("Lanes")]
        [Min(1)] public int laneCount = 2;
        public bool drawLaneMarkings = true;
        [Min(0.01f)] public float laneMarkingWidth = 0.15f;
        [Min(0.001f)] public float laneMarkingHeight = 0.02f;
        [Tooltip("Dash the interior lane dividers (between adjacent lanes).")]
        public bool dashLaneDividers = false;
        [Tooltip("Dash the outer edge lines (left/right edges of the road surface).")]
        public bool dashEdges = false;
        [Min(0.05f), Tooltip("Length (m) of one painted dash.")]
        public float laneMarkingDashLength = 3f;
        [Min(0f), Tooltip("Length (m) of the gap between dashes.")]
        public float laneMarkingDashGap = 6f;

        [Header("Curbs")]
        public bool drawCurbs = false;
        [Tooltip("Prefab/FBX root of the curb piece. Tiled along the spline. The length axis is auto-detected from the bounding box.")]
        public GameObject curbPrefab;
        public bool curbLeft = true;
        public bool curbRight = true;
        [Min(0.01f), Tooltip("Uniform scale applied to the source curb mesh before tiling.")]
        public float curbScale = 1f;
        [Tooltip("Lateral offset of the curb's center from the road's outer edge. Positive pushes the curb outward.")]
        public float curbLateralOffset = 0f;
        [Tooltip("Vertical offset added to the curb. By default the curb base sits on the road surface.")]
        public float curbVerticalOffset = 0f;
        [Min(0f), Tooltip("Gap (m) between consecutive curb tiles. Useful for avoiding clipping on tight curves.")]
        public float curbTileGap = 0f;
        [Tooltip("If true, alternate tiles are tinted with Curb Color Alt instead of Curb Color.")]
        public bool curbAlternateColors = false;
        [Tooltip("When true, tiles stretch slightly so they fit the road end-to-end. When false, tiles keep their exact natural size (Curb Scale × mesh length); any leftover at the end is left empty.")]
        public bool curbStretchToFit = true;

        [Header("Materials")]
        public Material surfaceMaterial;
        public Material laneMarkingMaterial;
        public Material curbMaterial;

        [Header("Colors")]
        [ColorUsage(false)] public Color surfaceColor = Color.white;
        [ColorUsage(false)] public Color laneMarkingColor = Color.white;
        [ColorUsage(false)] public Color curbColor = Color.white;
        [ColorUsage(false)] public Color curbColorAlt = Color.black;

        Mesh _mesh;
        MaterialPropertyBlock _mpb;

        Vector3[] _curbVerts;
        Vector3[] _curbNormals;
        Vector2[] _curbUVs;
        int[] _curbTris;
        Bounds _curbBounds;
        int _curbLengthAxis;
        int _curbHeightAxis;
        int _curbLateralAxis;
        int _curbBakedFromId;

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

        public void InvalidateCurbBake()
        {
            _curbBakedFromId = 0;
            _curbVerts = null;
        }

        struct Sample
        {
            public Vector3 P, T, U, R;
            public float halfWidth;
            public float t;
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
            var samples = new Sample[steps];
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                samples[i] = SampleAt(spline, t);
            }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var surfaceTris = new List<int>();

            float th = Mathf.Max(0f, thickness);

            BuildTopSurface(samples, length, verts, uvs, normals, surfaceTris);
            if (th > 0f) BuildThicknessGeometry(samples, length, th, verts, uvs, normals, surfaceTris);

            var markingTris = new List<int>();
            if (drawLaneMarkings) BuildLaneMarkings(samples, length, spline, verts, uvs, normals, markingTris);

            var leftEvenTris = new List<int>();
            var leftOddTris = new List<int>();
            var rightEvenTris = new List<int>();
            var rightOddTris = new List<int>();
            bool curbAvailable = drawCurbs && curbPrefab != null && EnsureCurbBaked();
            bool hasLeftCurb = curbAvailable && curbLeft;
            bool hasRightCurb = curbAvailable && curbRight;
            bool altColors = curbAlternateColors;
            if (hasLeftCurb) BuildCurb(verts, uvs, normals, leftEvenTris, leftOddTris, spline, length, false, altColors);
            if (hasRightCurb) BuildCurb(verts, uvs, normals, rightEvenTris, rightOddTris, spline, length, true, altColors);
            if (!altColors)
            {
                leftEvenTris.AddRange(leftOddTris); leftOddTris.Clear();
                rightEvenTris.AddRange(rightOddTris); rightOddTris.Clear();
            }

            bool hasMarkings = drawLaneMarkings && markingTris.Count > 0;

            var submeshes = new List<List<int>> { surfaceTris };
            var materials = new List<Material> { surfaceMaterial };
            int laneSlot = -1, leftSlot = -1, leftAltSlot = -1, rightSlot = -1, rightAltSlot = -1;
            if (hasMarkings)
            {
                laneSlot = submeshes.Count;
                submeshes.Add(markingTris);
                materials.Add(laneMarkingMaterial);
            }
            Material curbMat = curbMaterial != null ? curbMaterial : surfaceMaterial;
            if (hasLeftCurb && leftEvenTris.Count > 0)
            {
                leftSlot = submeshes.Count;
                submeshes.Add(leftEvenTris);
                materials.Add(curbMat);
            }
            if (hasLeftCurb && altColors && leftOddTris.Count > 0)
            {
                leftAltSlot = submeshes.Count;
                submeshes.Add(leftOddTris);
                materials.Add(curbMat);
            }
            if (hasRightCurb && rightEvenTris.Count > 0)
            {
                rightSlot = submeshes.Count;
                submeshes.Add(rightEvenTris);
                materials.Add(curbMat);
            }
            if (hasRightCurb && altColors && rightOddTris.Count > 0)
            {
                rightAltSlot = submeshes.Count;
                submeshes.Add(rightOddTris);
                materials.Add(curbMat);
            }

            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++)
                mesh.SetTriangles(submeshes[s], s);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterials = materials.ToArray();

            ApplyColor(mr, 0, surfaceColor);
            if (laneSlot >= 0) ApplyColor(mr, laneSlot, laneMarkingColor);
            if (leftSlot >= 0) ApplyColor(mr, leftSlot, curbColor);
            if (leftAltSlot >= 0) ApplyColor(mr, leftAltSlot, curbColorAlt);
            if (rightSlot >= 0) ApplyColor(mr, rightSlot, curbColor);
            if (rightAltSlot >= 0) ApplyColor(mr, rightAltSlot, curbColorAlt);
        }

        Sample SampleAt(Spline spline, float t)
        {
            SplineUtility.Evaluate(spline, t, out float3 pos, out float3 tan, out float3 up);
            Vector3 P = (Vector3)pos;
            Vector3 U = math.lengthsq(up) > 1e-8f ? ((Vector3)math.normalize(up)) : Vector3.up;
            Vector3 T;
            if (math.lengthsq(tan) > 1e-8f)
            {
                T = ((Vector3)math.normalize(tan));
            }
            else
            {
                float tNear = Mathf.Clamp01(t < 0.5f ? t + 0.001f : t - 0.001f);
                SplineUtility.Evaluate(spline, tNear, out float3 posN, out _, out _);
                Vector3 dir = ((Vector3)posN) - P;
                if (t > 0.5f) dir = -dir;
                T = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.forward;
            }
            Vector3 R = Vector3.Cross(U, T).normalized;
            return new Sample { P = P, T = T, U = U, R = R, halfWidth = GetWidthAt(t) * 0.5f, t = t };
        }

        void BuildTopSurface(Sample[] samples, float length,
            List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, List<int> tris)
        {
            int steps = samples.Length;
            int topBase = verts.Count;
            for (int i = 0; i < steps; i++)
            {
                var s = samples[i];
                verts.Add(s.P - s.R * s.halfWidth);
                verts.Add(s.P + s.R * s.halfWidth);
                float v = (s.t * length) / Mathf.Max(0.0001f, textureLengthScale);
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));
                normals.Add(s.U);
                normals.Add(s.U);
            }
            for (int i = 0; i < steps - 1; i++)
            {
                int b = topBase + i * 2;
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            }
        }

        void BuildThicknessGeometry(Sample[] samples, float length, float th,
            List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, List<int> tris)
        {
            int steps = samples.Length;

            int botBase = verts.Count;
            for (int i = 0; i < steps; i++)
            {
                var s = samples[i];
                Vector3 down = -s.U * th;
                verts.Add(s.P - s.R * s.halfWidth + down);
                verts.Add(s.P + s.R * s.halfWidth + down);
                float v = (s.t * length) / Mathf.Max(0.0001f, textureLengthScale);
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));
                normals.Add(-s.U);
                normals.Add(-s.U);
            }
            for (int i = 0; i < steps - 1; i++)
            {
                int b = botBase + i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
            }

            int leftBase = verts.Count;
            for (int i = 0; i < steps; i++)
            {
                var s = samples[i];
                Vector3 top = s.P - s.R * s.halfWidth;
                Vector3 bot = top - s.U * th;
                verts.Add(top);
                verts.Add(bot);
                float v = (s.t * length) / Mathf.Max(0.0001f, textureLengthScale);
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));
                normals.Add(-s.R);
                normals.Add(-s.R);
            }
            for (int i = 0; i < steps - 1; i++)
            {
                int b = leftBase + i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
            }

            int rightBase = verts.Count;
            for (int i = 0; i < steps; i++)
            {
                var s = samples[i];
                Vector3 top = s.P + s.R * s.halfWidth;
                Vector3 bot = top - s.U * th;
                verts.Add(top);
                verts.Add(bot);
                float v = (s.t * length) / Mathf.Max(0.0001f, textureLengthScale);
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));
                normals.Add(s.R);
                normals.Add(s.R);
            }
            for (int i = 0; i < steps - 1; i++)
            {
                int b = rightBase + i * 2;
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            }

            AddEndCap(samples[0], th, false, verts, uvs, normals, tris);
            AddEndCap(samples[steps - 1], th, true, verts, uvs, normals, tris);
        }

        void AddEndCap(Sample s, float th, bool isEnd,
            List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, List<int> tris)
        {
            int baseIdx = verts.Count;
            Vector3 topL = s.P - s.R * s.halfWidth;
            Vector3 topR = s.P + s.R * s.halfWidth;
            Vector3 botL = topL - s.U * th;
            Vector3 botR = topR - s.U * th;
            Vector3 n = isEnd ? s.T : -s.T;
            verts.Add(topL); verts.Add(topR); verts.Add(botL); verts.Add(botR);
            for (int i = 0; i < 4; i++) normals.Add(n);
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            if (isEnd)
            {
                tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
            }
            else
            {
                tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
            }
        }

        void BuildLaneMarkings(Sample[] samples, float length, Spline spline,
            List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, List<int> tris)
        {
            var offsets = new List<float> { -1f, 1f };
            for (int i = 1; i < laneCount; i++)
                offsets.Add(-1f + 2f * i / laneCount);

            foreach (var off in offsets)
            {
                bool isEdge = Mathf.Approximately(off, -1f) || Mathf.Approximately(off, 1f);
                bool dashed = isEdge ? dashEdges : dashLaneDividers;
                if (dashed)
                    BuildDashedMarkingStrip(spline, off, length, verts, uvs, normals, tris);
                else
                    BuildSolidMarkingStrip(samples, off, length, verts, uvs, normals, tris);
            }
        }

        void BuildSolidMarkingStrip(Sample[] samples, float off, float roadLength,
            List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, List<int> tris)
        {
            int steps = samples.Length;
            int baseIdx = verts.Count;
            for (int i = 0; i < steps; i++)
            {
                var s = samples[i];
                AddMarkingPair(verts, uvs, normals, s, off, s.t * roadLength);
            }
            for (int i = 0; i < steps - 1; i++)
            {
                int b = baseIdx + i * 2;
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            }
        }

        void BuildDashedMarkingStrip(Spline spline, float off, float roadLength,
            List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, List<int> tris)
        {
            float dashLen = Mathf.Max(0.05f, laneMarkingDashLength);
            float dashGap = Mathf.Max(0f, laneMarkingDashGap);
            float pitch = dashLen + dashGap;
            if (pitch < 0.05f) pitch = 0.05f;
            float pos = 0f;
            while (pos < roadLength)
            {
                float dashStart = pos;
                float dashEnd = Mathf.Min(pos + dashLen, roadLength);
                if (dashEnd - dashStart < 0.01f) break;
                int dashSteps = Mathf.Max(2, Mathf.CeilToInt((dashEnd - dashStart) * segmentsPerMeter));
                int baseIdx = verts.Count;
                for (int i = 0; i < dashSteps; i++)
                {
                    float frac = i / (float)(dashSteps - 1);
                    float arcLen = Mathf.Lerp(dashStart, dashEnd, frac);
                    arcLen = Mathf.Clamp(arcLen, 0f, roadLength - 1e-4f);
                    SplineUtility.GetPointAtLinearDistance(spline, 0f, arcLen, out float resultT);
                    Sample s = SampleAt(spline, Mathf.Clamp01(resultT));
                    AddMarkingPair(verts, uvs, normals, s, off, arcLen);
                }
                for (int i = 0; i < dashSteps - 1; i++)
                {
                    int b = baseIdx + i * 2;
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                }
                pos += pitch;
            }
        }

        void AddMarkingPair(List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals,
            Sample s, float off, float arcLen)
        {
            Vector3 center = s.P + s.R * (off * s.halfWidth) + s.U * laneMarkingHeight;
            Vector3 mLeft = center - s.R * (laneMarkingWidth * 0.5f);
            Vector3 mRight = center + s.R * (laneMarkingWidth * 0.5f);
            verts.Add(mLeft);
            verts.Add(mRight);
            float v = arcLen / Mathf.Max(0.0001f, textureLengthScale);
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(1f, v));
            normals.Add(s.U);
            normals.Add(s.U);
        }

        bool EnsureCurbBaked()
        {
            if (curbPrefab == null) return false;
            int id = curbPrefab.GetInstanceID();
            if (id == _curbBakedFromId && _curbVerts != null && _curbVerts.Length > 0) return true;
            return BakeCurbFromPrefab(id);
        }

        bool BakeCurbFromPrefab(int sourceId)
        {
            var inst = UnityEngine.Object.Instantiate(curbPrefab);
            inst.hideFlags = HideFlags.HideAndDontSave;
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            try
            {
                var verts = new List<Vector3>();
                var normals = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                var mfs = inst.GetComponentsInChildren<MeshFilter>(true);
                Matrix4x4 instInv = inst.transform.worldToLocalMatrix;
                foreach (var mf in mfs)
                {
                    var m = mf.sharedMesh;
                    if (m == null) continue;
                    if (!m.isReadable)
                    {
                        Debug.LogWarning($"[Road] Curb mesh '{m.name}' on '{mf.gameObject.name}' is not Read/Write enabled. Enable Read/Write in the FBX import settings.", this);
                        continue;
                    }
                    Matrix4x4 toRoot = instInv * mf.transform.localToWorldMatrix;
                    var srcVerts = m.vertices;
                    var srcNormals = m.normals;
                    var srcUVs = m.uv;
                    var srcTris = m.triangles;
                    int baseIdx = verts.Count;
                    for (int i = 0; i < srcVerts.Length; i++)
                        verts.Add(toRoot.MultiplyPoint3x4(srcVerts[i]));
                    if (srcNormals != null && srcNormals.Length == srcVerts.Length)
                    {
                        for (int i = 0; i < srcNormals.Length; i++)
                            normals.Add(toRoot.MultiplyVector(srcNormals[i]).normalized);
                    }
                    else
                    {
                        for (int i = 0; i < srcVerts.Length; i++) normals.Add(Vector3.up);
                    }
                    if (srcUVs != null && srcUVs.Length == srcVerts.Length) uvs.AddRange(srcUVs);
                    else for (int i = 0; i < srcVerts.Length; i++) uvs.Add(Vector2.zero);
                    for (int i = 0; i < srcTris.Length; i++) tris.Add(baseIdx + srcTris[i]);
                }
                if (verts.Count == 0) { _curbVerts = null; return false; }

                _curbVerts = verts.ToArray();
                _curbNormals = normals.ToArray();
                _curbUVs = uvs.ToArray();
                _curbTris = tris.ToArray();
                var b = new Bounds(_curbVerts[0], Vector3.zero);
                for (int i = 1; i < _curbVerts.Length; i++) b.Encapsulate(_curbVerts[i]);
                _curbBounds = b;

                int longest = 0;
                if (b.size.y > b.size.x && b.size.y >= b.size.z) longest = 1;
                else if (b.size.z > b.size.x && b.size.z > b.size.y) longest = 2;
                _curbLengthAxis = longest;
                if (longest == 0) { _curbHeightAxis = 1; _curbLateralAxis = 2; }
                else if (longest == 2) { _curbHeightAxis = 1; _curbLateralAxis = 0; }
                else { _curbHeightAxis = 0; _curbLateralAxis = 2; }

                _curbBakedFromId = sourceId;
                return true;
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(inst);
                else UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        void BuildCurb(List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals,
            List<int> trisEven, List<int> trisOdd,
            Spline spline, float roadLength, bool rightSide, bool alternateColors)
        {
            var sV = _curbVerts; var sN = _curbNormals; var sU = _curbUVs; var sT = _curbTris;
            Bounds b = _curbBounds;
            int lenA = _curbLengthAxis;
            int hgtA = _curbHeightAxis;
            int latA = _curbLateralAxis;
            float sourceLength = b.size[lenA];
            if (sourceLength < 0.0001f) return;

            float tileLengthNatural = sourceLength * curbScale;
            float gap = Mathf.Max(0f, curbTileGap);
            int tileCount;
            float actualPitch, actualTileLength;
            if (curbStretchToFit)
            {
                float effectivePitch = Mathf.Max(0.01f, tileLengthNatural + gap);
                tileCount = Mathf.Max(1, Mathf.RoundToInt(roadLength / effectivePitch));
                actualPitch = roadLength / tileCount;
                actualTileLength = Mathf.Max(0.01f, actualPitch - gap);
            }
            else
            {
                actualTileLength = Mathf.Max(0.01f, tileLengthNatural);
                actualPitch = actualTileLength + gap;
                tileCount = Mathf.Max(1, Mathf.FloorToInt((roadLength + gap) / actualPitch));
            }

            float mirrorSign = rightSide ? -1f : 1f;
            float sideSign = rightSide ? 1f : -1f;
            float heightMin = b.min[hgtA];

            for (int tile = 0; tile < tileCount; tile++)
            {
                List<int> targetTris = (alternateColors && (tile & 1) == 1) ? trisOdd : trisEven;
                int baseIdx = verts.Count;
                for (int vi = 0; vi < sV.Length; vi++)
                {
                    Vector3 sv = sV[vi];
                    float localL = (sv[lenA] - b.min[lenA]) / sourceLength;
                    float arcLen = tile * actualPitch + localL * actualTileLength;
                    arcLen = Mathf.Clamp(arcLen, 0f, roadLength - 1e-4f);

                    SplineUtility.GetPointAtLinearDistance(spline, 0f, arcLen, out float resultT);
                    resultT = Mathf.Clamp01(resultT);
                    Sample s = SampleAt(spline, resultT);

                    float vHeight = (sv[hgtA] - heightMin) * curbScale;
                    float vLateral = sv[latA] * curbScale * mirrorSign;

                    float lateralRoadOffset = sideSign * (s.halfWidth + curbLateralOffset);
                    Vector3 pos = s.P
                        + s.R * (lateralRoadOffset + vLateral)
                        + s.U * (vHeight + curbVerticalOffset);
                    verts.Add(pos);

                    Vector3 srcN = (vi < sN.Length) ? sN[vi] : Vector3.up;
                    float nLength = srcN[lenA];
                    float nHeight = srcN[hgtA];
                    float nLateral = srcN[latA] * mirrorSign;
                    Vector3 outN = s.T * nLength + s.U * nHeight + s.R * nLateral;
                    if (outN.sqrMagnitude > 1e-8f) outN.Normalize();
                    else outN = s.U;
                    normals.Add(outN);

                    uvs.Add(vi < sU.Length ? sU[vi] : new Vector2(localL, sv[latA]));
                }

                for (int ti = 0; ti < sT.Length; ti += 3)
                {
                    if (rightSide)
                    {
                        targetTris.Add(baseIdx + sT[ti]);
                        targetTris.Add(baseIdx + sT[ti + 1]);
                        targetTris.Add(baseIdx + sT[ti + 2]);
                    }
                    else
                    {
                        targetTris.Add(baseIdx + sT[ti]);
                        targetTris.Add(baseIdx + sT[ti + 2]);
                        targetTris.Add(baseIdx + sT[ti + 1]);
                    }
                }
            }
        }

        void ApplyColor(MeshRenderer mr, int slot, Color color)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(_mpb, slot);
            _mpb.SetColor("_Color", color);
            _mpb.SetColor("_BaseColor", color);
            mr.SetPropertyBlock(_mpb, slot);
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
