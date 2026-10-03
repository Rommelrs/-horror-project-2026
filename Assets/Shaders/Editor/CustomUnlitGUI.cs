using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Material inspector for Custom/CustomUnlit: the normal properties plus a
/// "Show through fog" section.
///
/// Enviro's fog is applied right after all OPAQUE geometry has been drawn, using the depth of what
/// it drew. Anything drawn later (the Transparent render queue, 3000) isn't fogged. With this
/// switched on the item is drawn after the fog and fades in with its distance from the camera:
/// hidden far away (lost in the fog), fully visible when close. It still hides behind walls.
/// Under the hood it sets Render Queue 3000 and alpha blending (and turns both back off again).
/// </summary>
public class CustomUnlitGUI : ShaderGUI
{
    const int AfterFogQueue = 3000;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);

        MaterialProperty showProp = FindProperty("_ShowThroughFog", properties);
        MaterialProperty nearProp = FindProperty("_RevealNear", properties);
        MaterialProperty farProp = FindProperty("_RevealFar", properties);

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        bool on = EditorGUILayout.Toggle(
            new GUIContent("Show through fog", "Draws this item after the fog effect and fades it in as the camera gets closer, so the fog can't wash it out."),
            showProp.floatValue > 0.5f);

        if (EditorGUI.EndChangeCheck())
        {
            foreach (Object t in materialEditor.targets)
                ApplyShowThroughFog((Material)t, on);
        }

        if (showProp.floatValue > 0.5f)
        {
            EditorGUI.indentLevel++;
            materialEditor.ShaderProperty(nearProp, new GUIContent("Fully visible within (m)", "Distance from the camera at which the item is fully visible."));
            materialEditor.ShaderProperty(farProp, new GUIContent("Hidden beyond (m)", "Distance from the camera beyond which the item is completely hidden. It fades in smoothly between the two distances."));
            EditorGUI.indentLevel--;

            if (farProp.floatValue < nearProp.floatValue)
                EditorGUILayout.HelpBox("'Hidden beyond' should be larger than 'Fully visible within'.", MessageType.Warning);
        }
    }

    /// <summary>Switches a CustomUnlit material between normal rendering and "show through fog" rendering.</summary>
    public static void ApplyShowThroughFog(Material m, bool on)
    {
        Undo.RecordObject(m, "Show through fog");

        m.SetFloat("_ShowThroughFog", on ? 1f : 0f);
        m.SetFloat("_SrcBlend", on ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
        m.SetFloat("_DstBlend", on ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
        m.renderQueue = on ? AfterFogQueue : -1; // -1 = use the shader's own queue

        EditorUtility.SetDirty(m);
    }
}
