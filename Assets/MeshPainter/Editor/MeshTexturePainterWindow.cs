using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class MeshTexturePainterWindow : EditorWindow
{
    private const string ShaderName = "MeshPainter/Texture Blend 4 Layers";
    private const string RootFolder = "Assets/MeshPainter";
    private const string GeneratedRootFolder = RootFolder + "/Generated";

    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private static readonly int BaseTexId = Shader.PropertyToID("_BaseTex");
    private static readonly int Layer1TexId = Shader.PropertyToID("_Layer1Tex");
    private static readonly int Layer2TexId = Shader.PropertyToID("_Layer2Tex");
    private static readonly int Layer3TexId = Shader.PropertyToID("_Layer3Tex");
    private static readonly int Layer4TexId = Shader.PropertyToID("_Layer4Tex");
    private static readonly int ControlTexId = Shader.PropertyToID("_ControlTex");

    private static readonly string[] LayerNames =
    {
        "Layer 1 (R)",
        "Layer 2 (G)",
        "Layer 3 (B)",
        "Layer 4 (A)"
    };

    private static readonly string[] SourceTextureProperties =
    {
        "_BaseTex",
        "_MainTex",
        "_BaseMap",
        "_BaseColorMap",
        "_Albedo",
        "_AlbedoMap",
        "_Diffuse",
        "_DiffuseMap"
    };

    private static readonly string[] SourceColorProperties =
    {
        "_BaseColor",
        "_Color",
        "_Tint"
    };


    private GameObject targetObject;
    private MeshRenderer targetRenderer;
    private MeshFilter targetFilter;
    private int materialSlot;

    private Material originalMaterial;
    private Color baseColor = Color.white;

    private Material workingMaterial;
    private Texture2D baseTexture;
    private Texture2D layer1Texture;
    private Texture2D layer2Texture;
    private Texture2D layer3Texture;
    private Texture2D layer4Texture;
    private Texture2D workingControlTexture;

    private int newControlResolution = 1024;
    private int selectedLayer;
    private float brushRadius = 1.0f;
    private float brushStrength = 0.55f;
    private bool eraseMode;
    private bool paintEnabled;

    private MeshCollider paintCollider;
    private bool createdTemporaryCollider;
    private Color[] lastStrokePixels;
    private bool strokeIsActive;
    private string generatedFolder;
    private string materialAssetPath;
    private string controlAssetPath;
    private string statusMessage = "Select a mesh object to begin.";

    [MenuItem("Tools/Mesh Texture Painter")]
    public static void Open()
    {
        MeshTexturePainterWindow window = GetWindow<MeshTexturePainterWindow>();
        window.titleContent = new GUIContent("Mesh Painter");
        window.minSize = new Vector2(360f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGui;
        RemoveTemporaryCollider();
    }

    private void OnSelectionChange()
    {
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        targetObject = (GameObject)EditorGUILayout.ObjectField("Mesh Object", targetObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            AssignTarget(targetObject);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selection"))
            {
                AssignTargetFromSelection();
            }

            GUI.enabled = HasValidTarget();
            if (GUILayout.Button("Frame"))
            {
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            GUI.enabled = true;
        }

        if (!HasValidTarget())
        {
            EditorGUILayout.HelpBox(GetTargetProblem(), MessageType.Info);
        }
        else
        {
            DrawMaterialSection();
            DrawTextureSection();
            DrawBrushSection();
            DrawSaveSection();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);
    }

    private void DrawMaterialSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Material", EditorStyles.boldLabel);

        Material[] materials = targetRenderer.sharedMaterials;
        int materialCount = Mathf.Max(1, materials == null ? 0 : materials.Length);
        materialSlot = EditorGUILayout.IntSlider("Material Slot", materialSlot, 0, materialCount - 1);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Working Material", workingMaterial, typeof(Material), false);
        }

        if (GUILayout.Button("Create / Assign Painted Material"))
        {
            PrepareWorkingMaterial(true);
        }
    }

    private void DrawTextureSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
EditorGUI.BeginChangeCheck();
        baseTexture = (Texture2D)EditorGUILayout.ObjectField("Base", baseTexture, typeof(Texture2D), false);
        layer1Texture = (Texture2D)EditorGUILayout.ObjectField("Layer 1 (R)", layer1Texture, typeof(Texture2D), false);
        layer2Texture = (Texture2D)EditorGUILayout.ObjectField("Layer 2 (G)", layer2Texture, typeof(Texture2D), false);
        layer3Texture = (Texture2D)EditorGUILayout.ObjectField("Layer 3 (B)", layer3Texture, typeof(Texture2D), false);
        layer4Texture = (Texture2D)EditorGUILayout.ObjectField("Layer 4 (A)", layer4Texture, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck())
        {
            ApplyTexturesToMaterial(workingControlTexture);
        }

        selectedLayer = GUILayout.Toolbar(selectedLayer, LayerNames);
        newControlResolution = EditorGUILayout.IntPopup("New Control Size", newControlResolution,
            new[] { "512", "1024", "2048", "4096" },
            new[] { 512, 1024, 2048, 4096 });

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("New Blank Control Map"))
            {
                CreateBlankControlTexture(newControlResolution);
                ApplyTexturesToMaterial(workingControlTexture);
                statusMessage = "Created a blank in-memory control map. Save it when you are ready.";
            }

            GUI.enabled = workingControlTexture != null;
            if (GUILayout.Button("Undo Last Stroke"))
            {
                UndoLastStroke();
            }
            GUI.enabled = true;
        }
    }

    private void DrawBrushSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);

        brushRadius = EditorGUILayout.Slider("Radius", brushRadius, 0.05f, 12f);
        brushStrength = EditorGUILayout.Slider("Strength", brushStrength, 0.01f, 1f);
        eraseMode = EditorGUILayout.ToggleLeft("Erase selected layer", eraseMode);

        if (!eraseMode && GetSelectedLayerTexture() == null)
        {
            EditorGUILayout.HelpBox("Assign a texture to " + LayerNames[selectedLayer] + " before painting that layer.", MessageType.Warning);
        }


        bool newPaintEnabled = GUILayout.Toggle(paintEnabled, "Paint In Scene View", "Button", GUILayout.Height(28f));
        if (newPaintEnabled != paintEnabled)
        {
            SetPaintEnabled(newPaintEnabled);
        }

        if (paintEnabled)
        {
            EditorGUILayout.HelpBox("Left-drag on the selected mesh. Hold Ctrl while painting to erase temporarily. Alt still orbits the Scene View.", MessageType.None);
        }
    }

    private void DrawSaveSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(workingControlTexture == null))
        {
            if (GUILayout.Button("Save Control Texture"))
            {
                Texture2D saved = SaveControlMapAsset();
                if (saved != null)
                {
                    ApplyTexturesToMaterial(saved);
                    AssetDatabase.SaveAssets();
                    statusMessage = "Saved control texture: " + controlAssetPath;
                }
            }
        }

        using (new EditorGUI.DisabledScope(!HasValidTarget()))
        {
            if (GUILayout.Button("Save Material Asset"))
            {
                SaveMaterialAsset();
            }

            if (GUILayout.Button("Save As Prefab"))
            {
                SaveAsPrefab();
            }
        }
    }

    private void AssignTargetFromSelection()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            statusMessage = "No GameObject is selected.";
            return;
        }

        MeshRenderer renderer = selected.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = selected.GetComponentInChildren<MeshRenderer>();
        }

        AssignTarget(renderer != null ? renderer.gameObject : selected);
    }

    private void AssignTarget(GameObject candidate)
    {
        RemoveTemporaryCollider();
        paintEnabled = false;
        targetObject = candidate;
        targetRenderer = null;
        targetFilter = null;
        originalMaterial = null;
        workingMaterial = null;
        workingControlTexture = null;
        lastStrokePixels = null;
        generatedFolder = null;
        materialAssetPath = null;
        controlAssetPath = null;

        if (targetObject == null)
        {
            statusMessage = "Select a mesh object to begin.";
            return;
        }

        targetRenderer = targetObject.GetComponent<MeshRenderer>();
        targetFilter = targetObject.GetComponent<MeshFilter>();
        materialSlot = 0;

        if (!HasValidTarget())
        {
            statusMessage = GetTargetProblem();
            return;
        }

        LoadTexturesFromCurrentMaterial();
        statusMessage = "Target ready: " + targetObject.name;
        SceneView.RepaintAll();
    }

    private bool HasValidTarget()
    {
        if (targetObject == null || targetRenderer == null || targetFilter == null)
        {
            return false;
        }

        Mesh mesh = targetFilter.sharedMesh;
        return mesh != null && mesh.uv != null && mesh.uv.Length > 0;
    }

    private string GetTargetProblem()
    {
        if (targetObject == null)
        {
            return "Select a GameObject with a MeshRenderer and MeshFilter.";
        }

        if (targetRenderer == null || targetFilter == null)
        {
            return "The selected object needs a MeshRenderer and MeshFilter. Skinned meshes are not supported by this MVP.";
        }

        Mesh mesh = targetFilter.sharedMesh;
        if (mesh == null)
        {
            return "The selected MeshFilter has no mesh.";
        }

        if (mesh.uv == null || mesh.uv.Length == 0)
        {
            return "The selected mesh has no UV0 coordinates. UVs are required for texture painting.";
        }

        return "Target is not ready.";
    }

private void LoadTexturesFromCurrentMaterial()
    {
        Material[] materials = targetRenderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            originalMaterial = null;
            baseColor = Color.white;
            baseTexture = null;
            layer1Texture = null;
            layer2Texture = null;
            layer3Texture = null;
            layer4Texture = null;
            return;
        }

        materialSlot = Mathf.Clamp(materialSlot, 0, materials.Length - 1);
        originalMaterial = materials[materialSlot];
        baseColor = Color.white;
        if (originalMaterial == null)
        {
            return;
        }

        if (originalMaterial.shader != null && originalMaterial.shader.name == ShaderName)
        {
            workingMaterial = originalMaterial;
            baseColor = GetColor(originalMaterial, BaseColorId, Color.white);
            baseTexture = GetTexture(originalMaterial, BaseTexId);
            layer1Texture = GetTexture(originalMaterial, Layer1TexId);
            layer2Texture = GetTexture(originalMaterial, Layer2TexId);
            layer3Texture = GetTexture(originalMaterial, Layer3TexId);
            layer4Texture = GetTexture(originalMaterial, Layer4TexId);

            Texture2D existingControl = GetTexture(originalMaterial, ControlTexId);
            if (existingControl != null)
            {
                CopyControlTexture(existingControl);
            }
        }
        else
        {
            baseColor = FindFirstColor(originalMaterial, SourceColorProperties, Color.white);
            baseTexture = FindFirstTexture(originalMaterial, SourceTextureProperties);
            layer1Texture = null;
            layer2Texture = null;
            layer3Texture = null;
            layer4Texture = null;
        }
    }

    private static Texture2D GetTexture(Material material, int propertyId)
    {
        if (material == null || !material.HasProperty(propertyId))
        {
            return null;
        }

        return material.GetTexture(propertyId) as Texture2D;
    }

    private static Color GetColor(Material material, int propertyId, Color fallback)
    {
        if (material == null || !material.HasProperty(propertyId))
        {
            return fallback;
        }

        return material.GetColor(propertyId);
    }

    private static Texture2D FindFirstTexture(Material material, string[] propertyNames)
    {
        if (material == null)
        {
            return null;
        }

        for (int i = 0; i < propertyNames.Length; i++)
        {
            string propertyName = propertyNames[i];
            if (!material.HasProperty(propertyName))
            {
                continue;
            }

            Texture2D texture = material.GetTexture(propertyName) as Texture2D;
            if (texture != null)
            {
                return texture;
            }
        }

        return material.mainTexture as Texture2D;
    }

    private static Color FindFirstColor(Material material, string[] propertyNames, Color fallback)
    {
        if (material == null)
        {
            return fallback;
        }

        for (int i = 0; i < propertyNames.Length; i++)
        {
            string propertyName = propertyNames[i];
            if (material.HasProperty(propertyName))
            {
                return material.GetColor(propertyName);
            }
        }

        return fallback;
    }

    private Texture2D GetSelectedLayerTexture()
    {
        switch (selectedLayer)
        {
            case 0:
                return layer1Texture;
            case 1:
                return layer2Texture;
            case 2:
                return layer3Texture;
            case 3:
                return layer4Texture;
            default:
                return null;
        }
    }


    private void SetPaintEnabled(bool enabled)
    {
        if (!enabled)
        {
            paintEnabled = false;
            RemoveTemporaryCollider();
            statusMessage = "Painting disabled.";
            SceneView.RepaintAll();
            return;
        }

        if (!HasValidTarget())
        {
            statusMessage = GetTargetProblem();
            paintEnabled = false;
            return;
        }

        if (workingMaterial == null)
        {
            PrepareWorkingMaterial(true);
        }

        if (workingControlTexture == null)
        {
            CreateBlankControlTexture(newControlResolution);
            ApplyTexturesToMaterial(workingControlTexture);
        }

        if (!EnsurePaintCollider())
        {
            paintEnabled = false;
            return;
        }

        paintEnabled = true;
        statusMessage = "Painting enabled for " + targetObject.name + ".";
        SceneView.RepaintAll();
    }

private void PrepareWorkingMaterial(bool assignToRenderer)
    {
        if (!HasValidTarget())
        {
            statusMessage = GetTargetProblem();
            return;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            statusMessage = "Could not find shader: " + ShaderName;
            return;
        }

        EnsureGeneratedPaths();
        Material assetMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
        if (assetMaterial == null)
        {
            assetMaterial = new Material(shader);
            assetMaterial.name = Path.GetFileNameWithoutExtension(materialAssetPath);
            AssetDatabase.CreateAsset(assetMaterial, materialAssetPath);
        }
        else
        {
            assetMaterial.shader = shader;
        }

        workingMaterial = assetMaterial;

        if (originalMaterial != null && originalMaterial.shader != null && originalMaterial.shader.name != ShaderName)
        {
            if (baseTexture == null)
            {
                baseTexture = FindFirstTexture(originalMaterial, SourceTextureProperties);
            }

            baseColor = FindFirstColor(originalMaterial, SourceColorProperties, baseColor);
        }

        if (workingMaterial.HasProperty(TintId))
        {
            workingMaterial.SetColor(TintId, Color.white);
        }

        if (workingMaterial.HasProperty(BaseColorId))
        {
            workingMaterial.SetColor(BaseColorId, baseColor);
        }

        if (workingControlTexture == null)
        {
            CreateBlankControlTexture(newControlResolution);
        }

        ApplyTexturesToMaterial(workingControlTexture);

        if (assignToRenderer)
        {
            AssignWorkingMaterialToRenderer();
        }

        AssetDatabase.SaveAssets();
        statusMessage = "Painted material ready: " + materialAssetPath;
    }

    private void AssignWorkingMaterialToRenderer()
    {
        if (targetRenderer == null || workingMaterial == null)
        {
            return;
        }

        Undo.RecordObject(targetRenderer, "Assign Painted Material");
        Material[] materials = targetRenderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            materials = new[] { workingMaterial };
            materialSlot = 0;
        }
        else
        {
            materialSlot = Mathf.Clamp(materialSlot, 0, materials.Length - 1);
            materials[materialSlot] = workingMaterial;
        }

        targetRenderer.sharedMaterials = materials;
        EditorUtility.SetDirty(targetRenderer);
        if (targetObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(targetObject.scene);
        }
    }

private void ApplyTexturesToMaterial(Texture controlTexture)
    {
        if (workingMaterial == null)
        {
            return;
        }

        if (workingMaterial.HasProperty(TintId))
        {
            workingMaterial.SetColor(TintId, Color.white);
        }

        if (workingMaterial.HasProperty(BaseColorId))
        {
            workingMaterial.SetColor(BaseColorId, baseColor);
        }

        if (baseTexture != null)
        {
            workingMaterial.SetTexture(BaseTexId, baseTexture);
        }

        if (layer1Texture != null)
        {
            workingMaterial.SetTexture(Layer1TexId, layer1Texture);
        }

        if (layer2Texture != null)
        {
            workingMaterial.SetTexture(Layer2TexId, layer2Texture);
        }

        if (layer3Texture != null)
        {
            workingMaterial.SetTexture(Layer3TexId, layer3Texture);
        }

        if (layer4Texture != null)
        {
            workingMaterial.SetTexture(Layer4TexId, layer4Texture);
        }

        if (controlTexture != null)
        {
            workingMaterial.SetTexture(ControlTexId, controlTexture);
        }

        EditorUtility.SetDirty(workingMaterial);
        SceneView.RepaintAll();
    }

    private void CreateBlankControlTexture(int resolution)
    {
        resolution = Mathf.Clamp(resolution, 32, 4096);
        workingControlTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
        workingControlTexture.name = targetObject != null ? SanitizeName(targetObject.name) + "_Control_Working" : "MeshPaint_Control_Working";
        workingControlTexture.wrapMode = TextureWrapMode.Clamp;
        workingControlTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[resolution * resolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        workingControlTexture.SetPixels(pixels);
        workingControlTexture.Apply(false, false);
        lastStrokePixels = null;
    }

    private void CopyControlTexture(Texture2D source)
    {
        if (source == null)
        {
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (!string.IsNullOrEmpty(sourcePath))
        {
            ConfigureControlTextureImporter(sourcePath);
            source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            controlAssetPath = sourcePath;
        }

        try
        {
            int resolution = Mathf.Max(32, Mathf.Min(source.width, source.height));
            workingControlTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
            workingControlTexture.name = source.name + "_Working";
            workingControlTexture.wrapMode = TextureWrapMode.Clamp;
            workingControlTexture.filterMode = FilterMode.Bilinear;

            Color[] sourcePixels = source.GetPixels(0, 0, resolution, resolution);
            workingControlTexture.SetPixels(sourcePixels);
            workingControlTexture.Apply(false, false);
            newControlResolution = resolution;
        }
        catch (Exception exception)
        {
            CreateBlankControlTexture(newControlResolution);
            statusMessage = "Could not read existing control texture, so a blank map was created. " + exception.Message;
        }
    }

    private bool EnsurePaintCollider()
    {
        if (!HasValidTarget())
        {
            statusMessage = GetTargetProblem();
            return false;
        }

        Mesh mesh = targetFilter.sharedMesh;
        MeshCollider[] colliders = targetObject.GetComponents<MeshCollider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].sharedMesh == mesh)
            {
                paintCollider = colliders[i];
                createdTemporaryCollider = false;
                return true;
            }
        }

        paintCollider = targetObject.AddComponent<MeshCollider>();
        paintCollider.sharedMesh = mesh;
        paintCollider.convex = false;
        paintCollider.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor;
        createdTemporaryCollider = true;
        return true;
    }

    private void RemoveTemporaryCollider()
    {
        if (createdTemporaryCollider && paintCollider != null)
        {
            DestroyImmediate(paintCollider);
        }

        paintCollider = null;
        createdTemporaryCollider = false;
    }

    private void OnSceneGui(SceneView sceneView)
    {
        if (!paintEnabled || !HasValidTarget() || paintCollider == null)
        {
            return;
        }

        Event current = Event.current;
        if (current == null)
        {
            return;
        }

        if (current.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        if (!paintCollider.Raycast(ray, out RaycastHit hit, 100000f))
        {
            return;
        }

        if (current.type == EventType.Repaint)
        {
            Handles.color = eraseMode || current.control || current.command
                ? new Color(1f, 0.25f, 0.2f, 0.95f)
                : new Color(0.25f, 0.75f, 1f, 0.95f);
            Handles.DrawWireDisc(hit.point, hit.normal, brushRadius, 2f);
        }

        bool paintEvent = current.button == 0
            && !current.alt
            && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag);

        if (!paintEvent)
        {
            if (current.type == EventType.MouseUp)
            {
                strokeIsActive = false;
            }
            return;
        }

        if (current.type == EventType.MouseDown || !strokeIsActive)
        {
            CaptureStrokeUndo();
            strokeIsActive = true;
        }

        bool erase = eraseMode || current.control || current.command;
        PaintAt(hit, erase);
        current.Use();
    }

    private void CaptureStrokeUndo()
    {
        if (workingControlTexture == null)
        {
            return;
        }

        try
        {
            lastStrokePixels = workingControlTexture.GetPixels();
        }
        catch
        {
            lastStrokePixels = null;
        }
    }

    private void UndoLastStroke()
    {
        if (workingControlTexture == null || lastStrokePixels == null || lastStrokePixels.Length != workingControlTexture.width * workingControlTexture.height)
        {
            statusMessage = "No stroke is available to undo.";
            return;
        }

        workingControlTexture.SetPixels(lastStrokePixels);
        workingControlTexture.Apply(false, false);
        ApplyTexturesToMaterial(workingControlTexture);
        statusMessage = "Restored the previous stroke state.";
    }

    private void PaintAt(RaycastHit hit, bool erase)
    {
        if (workingControlTexture == null || workingMaterial == null)
        {
            return;
        }

        if (!erase && GetSelectedLayerTexture() == null)
        {
            statusMessage = "Assign a texture to " + LayerNames[selectedLayer] + " before painting.";
            Repaint();
            return;
        }


        int width = workingControlTexture.width;
        int height = workingControlTexture.height;
        Vector2 uv = hit.textureCoord;
        float centerX = Mathf.Repeat(uv.x, 1f) * (width - 1);
        float centerY = Mathf.Repeat(uv.y, 1f) * (height - 1);
        float pixelsPerWorld = EstimatePixelsPerWorld(hit);
        int radiusPixels = Mathf.Max(1, Mathf.CeilToInt(brushRadius * pixelsPerWorld));

        int minX = Mathf.Clamp(Mathf.FloorToInt(centerX - radiusPixels), 0, width - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(centerX + radiusPixels), 0, width - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(centerY - radiusPixels), 0, height - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(centerY + radiusPixels), 0, height - 1);
        int blockWidth = maxX - minX + 1;
        int blockHeight = maxY - minY + 1;

        Color[] pixels = workingControlTexture.GetPixels(minX, minY, blockWidth, blockHeight);
        float sqrRadius = radiusPixels * radiusPixels;
        bool changed = false;

        for (int y = 0; y < blockHeight; y++)
        {
            for (int x = 0; x < blockWidth; x++)
            {
                float px = minX + x;
                float py = minY + y;
                float dx = px - centerX;
                float dy = py - centerY;
                float sqrDistance = dx * dx + dy * dy;
                if (sqrDistance > sqrRadius)
                {
                    continue;
                }

                float normalizedDistance = Mathf.Sqrt(sqrDistance) / radiusPixels;
                float falloff = 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);
                float amount = Mathf.Clamp01(brushStrength * falloff);
                int index = y * blockWidth + x;
                pixels[index] = PaintPixel(pixels[index], selectedLayer, amount, erase);
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        workingControlTexture.SetPixels(minX, minY, blockWidth, blockHeight, pixels);
        workingControlTexture.Apply(false, false);
        workingMaterial.SetTexture(ControlTexId, workingControlTexture);
        EditorUtility.SetDirty(workingMaterial);
        SceneView.RepaintAll();
    }

    private static Color PaintPixel(Color color, int layer, float amount, bool erase)
    {
        amount = Mathf.Clamp01(amount);
        float[] weights = { color.r, color.g, color.b, color.a };
        layer = Mathf.Clamp(layer, 0, weights.Length - 1);

        if (erase)
        {
            weights[layer] = Mathf.Lerp(weights[layer], 0f, amount);
        }
        else
        {
            weights[layer] = Mathf.Lerp(weights[layer], 1f, amount);
            float total = weights[0] + weights[1] + weights[2] + weights[3];
            if (total > 1f)
            {
                float overflow = total - 1f;
                float otherTotal = 0f;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (i != layer)
                    {
                        otherTotal += weights[i];
                    }
                }

                if (otherTotal > 0.0001f)
                {
                    for (int i = 0; i < weights.Length; i++)
                    {
                        if (i == layer)
                        {
                            continue;
                        }

                        float reduction = overflow * (weights[i] / otherTotal);
                        weights[i] = Mathf.Max(0f, weights[i] - reduction);
                    }
                }
            }
        }

        return new Color(
            Mathf.Clamp01(weights[0]),
            Mathf.Clamp01(weights[1]),
            Mathf.Clamp01(weights[2]),
            Mathf.Clamp01(weights[3]));
    }

    private float EstimatePixelsPerWorld(RaycastHit hit)
    {
        Mesh mesh = targetFilter != null ? targetFilter.sharedMesh : null;
        if (mesh == null || mesh.uv == null || mesh.uv.Length == 0)
        {
            return FallbackPixelsPerWorld();
        }

        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        int triangleStart = hit.triangleIndex * 3;
        if (triangleStart < 0 || triangleStart + 2 >= triangles.Length)
        {
            return FallbackPixelsPerWorld();
        }

        int a = triangles[triangleStart];
        int b = triangles[triangleStart + 1];
        int c = triangles[triangleStart + 2];
        float total = 0f;
        int count = 0;
        AccumulateUvWorldRatio(vertices, uvs, a, b, ref total, ref count);
        AccumulateUvWorldRatio(vertices, uvs, b, c, ref total, ref count);
        AccumulateUvWorldRatio(vertices, uvs, c, a, ref total, ref count);

        if (count == 0)
        {
            return FallbackPixelsPerWorld();
        }

        return Mathf.Clamp(total / count, 1f, workingControlTexture.width * 4f);
    }

    private void AccumulateUvWorldRatio(Vector3[] vertices, Vector2[] uvs, int a, int b, ref float total, ref int count)
    {
        if (a < 0 || b < 0 || a >= vertices.Length || b >= vertices.Length || a >= uvs.Length || b >= uvs.Length)
        {
            return;
        }

        Vector3 worldA = targetObject.transform.TransformPoint(vertices[a]);
        Vector3 worldB = targetObject.transform.TransformPoint(vertices[b]);
        float worldDistance = Vector3.Distance(worldA, worldB);
        float uvDistance = Vector2.Distance(uvs[a], uvs[b]) * workingControlTexture.width;
        if (worldDistance <= 0.0001f || uvDistance <= 0.0001f)
        {
            return;
        }

        total += uvDistance / worldDistance;
        count++;
    }

    private float FallbackPixelsPerWorld()
    {
        Bounds bounds = targetRenderer != null ? targetRenderer.bounds : new Bounds(Vector3.zero, Vector3.one);
        float size = Mathf.Max(0.0001f, bounds.size.magnitude);
        return workingControlTexture != null ? workingControlTexture.width / size : 128f;
    }

    private Texture2D SaveControlMapAsset()
    {
        if (workingControlTexture == null)
        {
            statusMessage = "There is no control texture to save.";
            return null;
        }

        EnsureGeneratedPaths();
        byte[] png = workingControlTexture.EncodeToPNG();
        string fullPath = ToFullPath(controlAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, png);
        AssetDatabase.ImportAsset(controlAssetPath);
        ConfigureControlTextureImporter(controlAssetPath);
        Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(controlAssetPath);
        return savedTexture;
    }

    private void SaveMaterialAsset()
    {
        if (!HasValidTarget())
        {
            statusMessage = GetTargetProblem();
            return;
        }

        if (workingMaterial == null)
        {
            PrepareWorkingMaterial(true);
        }

        Texture2D savedControl = SaveControlMapAsset();
        if (savedControl != null)
        {
            ApplyTexturesToMaterial(savedControl);
        }

        AssignWorkingMaterialToRenderer();
        AssetDatabase.SaveAssets();
        statusMessage = "Saved material asset: " + materialAssetPath;
    }

    private void SaveAsPrefab()
    {
        if (!HasValidTarget())
        {
            statusMessage = GetTargetProblem();
            return;
        }

        SaveMaterialAsset();
        EnsureGeneratedPaths();
        string prefabPath = generatedFolder + "/" + SanitizeName(targetObject.name) + "_Painted.prefab";

        bool wasPainting = paintEnabled;
        RemoveTemporaryCollider();
        bool success;
        PrefabUtility.SaveAsPrefabAsset(targetObject, prefabPath, out success);
        if (wasPainting)
        {
            EnsurePaintCollider();
        }

        statusMessage = success ? "Saved prefab: " + prefabPath : "Prefab save failed.";
    }

    private void EnsureGeneratedPaths()
    {
        string safeName = targetObject != null ? SanitizeName(targetObject.name) : "PaintedMesh";
        EnsureFolder(RootFolder);
        EnsureFolder(GeneratedRootFolder);
        generatedFolder = GeneratedRootFolder + "/" + safeName;
        EnsureFolder(generatedFolder);
        materialAssetPath = generatedFolder + "/" + safeName + "_Painted.mat";
        controlAssetPath = generatedFolder + "/" + safeName + "_Control.png";
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string[] parts = assetFolder.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
        {
            return;
        }

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static void ConfigureControlTextureImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            changed = true;
        }

        if (importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }

        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            changed = true;
        }
        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
        }

    private static string ToFullPath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, assetPath).Replace('/', Path.DirectorySeparatorChar);
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "PaintedMesh";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = value;
        for (int i = 0; i < invalid.Length; i++)
        {
            sanitized = sanitized.Replace(invalid[i], '_');
        }

        return sanitized.Replace(' ', '_');
    }
}
