using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace RoadTool.EditorTools
{
    public class RoadCreatorWindow : EditorWindow
    {
        [MenuItem("Tools/Road Tool/Road Creator")]
        public static void Open()
        {
            var w = GetWindow<RoadCreatorWindow>("Road Creator");
            w.minSize = new Vector2(280, 220);
        }

        float _width = 6f;
        int _lanes = 2;
        float _segmentsPerMeter = 2f;
        bool _drawMarkings = true;
        Material _surface;
        Material _markings;

        void OnEnable()
        {
            _surface = AssetDatabase.LoadAssetAtPath<Material>("Assets/RoadTool/Materials/Road_Asphalt.mat");
            _markings = AssetDatabase.LoadAssetAtPath<Material>("Assets/RoadTool/Materials/Road_LaneMarking.mat");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Create New Road", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates a GameObject with a SplineContainer and Road component at the scene origin. " +
                "Edit the spline knots in the Scene view to shape the road.",
                MessageType.Info);

            _width = EditorGUILayout.FloatField("Default Width (m)", _width);
            _lanes = EditorGUILayout.IntSlider("Lane Count", _lanes, 1, 6);
            _segmentsPerMeter = EditorGUILayout.Slider("Segments / Meter", _segmentsPerMeter, 0.25f, 8f);
            _drawMarkings = EditorGUILayout.Toggle("Draw Lane Markings", _drawMarkings);
            _surface = (Material)EditorGUILayout.ObjectField("Surface Material", _surface, typeof(Material), false);
            _markings = (Material)EditorGUILayout.ObjectField("Lane Marking Material", _markings, typeof(Material), false);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_surface == null))
            {
                if (GUILayout.Button("Create Road in Scene", GUILayout.Height(32))) Create();
            }
            if (_surface == null)
                EditorGUILayout.HelpBox("Assign a Surface Material first.", MessageType.Warning);
        }

        void Create()
        {
            var go = new GameObject("Road",
                typeof(SplineContainer),
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(Road));

            // Place in front of scene camera if possible
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                var cam = sv.camera;
                go.transform.position = cam.transform.position + cam.transform.forward * 8f;
                go.transform.position = new Vector3(go.transform.position.x, 0f, go.transform.position.z);
            }

            var sc = go.GetComponent<SplineContainer>();
            sc.Spline.Add(new BezierKnot(new float3(-10f, 0f, 0f)));
            sc.Spline.Add(new BezierKnot(new float3(10f, 0f, 0f)));

            var r = go.GetComponent<Road>();
            r.splineContainer = sc;
            r.defaultWidth = _width;
            r.laneCount = _lanes;
            r.segmentsPerMeter = _segmentsPerMeter;
            r.drawLaneMarkings = _drawMarkings;
            r.surfaceMaterial = _surface;
            r.laneMarkingMaterial = _markings;
            r.Rebuild();

            Undo.RegisterCreatedObjectUndo(go, "Create Road");
            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
    }
}
