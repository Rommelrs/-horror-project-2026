using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace RoadTool.EditorTools
{
    [CustomEditor(typeof(Road))]
    public class RoadEditor : Editor
    {
        bool _dirty;

        void OnEnable()
        {
            Spline.Changed += OnSplineChanged;
            EditorApplication.update += OnUpdate;
        }

        void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
            EditorApplication.update -= OnUpdate;
        }

        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            _dirty = true;
        }

        void OnUpdate()
        {
            if (!_dirty || target == null) return;
            _dirty = false;
            ((Road)target).Rebuild();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            var road = (Road)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Mesh"))
                {
                    road.InvalidateCurbBake();
                    road.Rebuild();
                    EditorUtility.SetDirty(road);
                }
                if (GUILayout.Button("Sync Knot Widths"))
                {
                    if (road.splineContainer != null && road.splineContainer.Spline != null)
                    {
                        int n = road.splineContainer.Spline.Count;
                        Undo.RecordObject(road, "Sync Knot Widths");
                        while (road.knotWidths.Count < n) road.knotWidths.Add(road.defaultWidth);
                        while (road.knotWidths.Count > n) road.knotWidths.RemoveAt(road.knotWidths.Count - 1);
                        road.Rebuild();
                        EditorUtility.SetDirty(road);
                    }
                }
                if (GUILayout.Button("Re-bake Curb"))
                {
                    road.InvalidateCurbBake();
                    road.Rebuild();
                    EditorUtility.SetDirty(road);
                }
            }

            if (road.splineContainer != null && road.splineContainer.Spline != null)
            {
                int knots = road.splineContainer.Spline.Count;
                if (road.knotWidths.Count != knots && road.knotWidths.Count > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"knotWidths has {road.knotWidths.Count} entries, but the spline has {knots} knots. " +
                        "Click 'Sync Knot Widths' to resize.",
                        MessageType.Warning);
                }
            }

            if (road.drawCurbs && road.curbPrefab != null)
            {
                bool anyUnreadable = false;
                var mfs = road.curbPrefab.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in mfs)
                    if (mf.sharedMesh != null && !mf.sharedMesh.isReadable) { anyUnreadable = true; break; }
                if (anyUnreadable)
                {
                    EditorGUILayout.HelpBox(
                        "The assigned curb prefab has meshes without Read/Write enabled. " +
                        "Open the FBX import settings and tick 'Read/Write' under Model.",
                        MessageType.Warning);
                }
            }
        }
    }
}
