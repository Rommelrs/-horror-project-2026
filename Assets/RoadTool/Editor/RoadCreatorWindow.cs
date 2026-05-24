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
            w.minSize = new Vector2(300, 460);
        }

        float _width = 6f;
        float _thickness = 0.1f;
        int _lanes = 2;
        float _segmentsPerMeter = 2f;
        bool _drawMarkings = true;
        bool _dashDividers = true;
        bool _dashEdges = false;
        float _dashLength = 3f;
        float _dashGap = 6f;
        bool _drawCurbs = true;
        float _curbTileGap = 0f;
        bool _curbAlternate = false;
        Material _surface;
        Material _markings;
        Material _curb;
        GameObject _curbPrefab;
        Color _surfaceColor = Color.white;
        Color _markingColor = Color.white;
        Color _curbColor = Color.white;
        Color _curbColorAlt = Color.black;
        Vector2 _scroll;

        void OnEnable()
        {
            _surface = AssetDatabase.LoadAssetAtPath<Material>("Assets/RoadTool/Materials/Road_Asphalt.mat");
            _markings = AssetDatabase.LoadAssetAtPath<Material>("Assets/RoadTool/Materials/Road_LaneMarking.mat");
            _curb = AssetDatabase.LoadAssetAtPath<Material>("Assets/RoadTool/Materials/Road_Curb.mat");
            _curbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RoadTool/Materials/road_curb_mesh.fbx");
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Create New Road", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates a GameObject with a SplineContainer and Road component near the scene camera. " +
                "Edit the spline knots in the Scene view to shape the road.",
                MessageType.Info);

            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
            _width = EditorGUILayout.FloatField("Default Width (m)", _width);
            _thickness = EditorGUILayout.Slider("Thickness Y (m)", _thickness, 0f, 5f);
            _lanes = EditorGUILayout.IntSlider("Lane Count", _lanes, 1, 6);
            _segmentsPerMeter = EditorGUILayout.Slider("Segments / Meter", _segmentsPerMeter, 0.25f, 8f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lane Markings", EditorStyles.boldLabel);
            _drawMarkings = EditorGUILayout.Toggle("Draw Lane Markings", _drawMarkings);
            using (new EditorGUI.DisabledScope(!_drawMarkings))
            {
                _dashDividers = EditorGUILayout.Toggle("Dash Lane Dividers", _dashDividers);
                _dashEdges = EditorGUILayout.Toggle("Dash Outer Edges", _dashEdges);
                _dashLength = EditorGUILayout.Slider("Dash Length (m)", _dashLength, 0.1f, 20f);
                _dashGap = EditorGUILayout.Slider("Dash Gap (m)", _dashGap, 0f, 20f);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Curbs", EditorStyles.boldLabel);
            _drawCurbs = EditorGUILayout.Toggle("Draw Curbs", _drawCurbs);
            using (new EditorGUI.DisabledScope(!_drawCurbs))
            {
                _curbPrefab = (GameObject)EditorGUILayout.ObjectField("Curb Prefab", _curbPrefab, typeof(GameObject), false);
                _curbTileGap = EditorGUILayout.Slider("Tile Gap (m)", _curbTileGap, 0f, 5f);
                _curbAlternate = EditorGUILayout.Toggle("Alternate Tile Colors", _curbAlternate);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Materials", EditorStyles.boldLabel);
            _surface = (Material)EditorGUILayout.ObjectField("Surface", _surface, typeof(Material), false);
            _markings = (Material)EditorGUILayout.ObjectField("Lane Markings", _markings, typeof(Material), false);
            _curb = (Material)EditorGUILayout.ObjectField("Curb", _curb, typeof(Material), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
            _surfaceColor = EditorGUILayout.ColorField("Surface Color", _surfaceColor);
            _markingColor = EditorGUILayout.ColorField("Marking Color", _markingColor);
            _curbColor = EditorGUILayout.ColorField("Curb Color", _curbColor);
            using (new EditorGUI.DisabledScope(!_curbAlternate))
                _curbColorAlt = EditorGUILayout.ColorField("Curb Color Alt", _curbColorAlt);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_surface == null))
            {
                if (GUILayout.Button("Create Road in Scene", GUILayout.Height(32))) Create();
            }
            if (_surface == null)
                EditorGUILayout.HelpBox("Assign a Surface Material first.", MessageType.Warning);

            EditorGUILayout.EndScrollView();
        }

        void Create()
        {
            var go = new GameObject("Road",
                typeof(SplineContainer),
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(Road));

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
            r.thickness = _thickness;
            r.laneCount = _lanes;
            r.segmentsPerMeter = _segmentsPerMeter;
            r.drawLaneMarkings = _drawMarkings;
            r.dashLaneDividers = _dashDividers;
            r.dashEdges = _dashEdges;
            r.laneMarkingDashLength = _dashLength;
            r.laneMarkingDashGap = _dashGap;
            r.drawCurbs = _drawCurbs;
            r.curbPrefab = _curbPrefab;
            r.curbTileGap = _curbTileGap;
            r.curbAlternateColors = _curbAlternate;
            r.surfaceMaterial = _surface;
            r.laneMarkingMaterial = _markings;
            r.curbMaterial = _curb != null ? _curb : _surface;
            r.surfaceColor = _surfaceColor;
            r.laneMarkingColor = _markingColor;
            r.curbColor = _curbColor;
            r.curbColorAlt = _curbColorAlt;
            r.Rebuild();

            Undo.RegisterCreatedObjectUndo(go, "Create Road");
            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
    }
}
