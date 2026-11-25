#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Short inspection helpers for automation (Claude): dump an object's scripts to a text file, photograph a spot.</summary>
public static class AgentTools
{
    public static string Dir { get { string d = Path.Combine(Path.GetTempPath(), "HorrorAgent").Replace((char)92, (char)47) + "/"; Directory.CreateDirectory(d); return d; } }

    public static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;

    public static Transform Find(string endsWith)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (("/" + PathOf(t)).EndsWith("/" + endsWith)) return t;
        return null;
    }

    public static void Dump(params string[] names)
    {
        var sb = new StringBuilder();
        foreach (var name in names)
        {
            var t = Find(name);
            if (t == null) { sb.AppendLine("#### NOT FOUND " + name); continue; }
            sb.AppendLine("#### " + PathOf(t) + " active " + t.gameObject.activeInHierarchy + " pos " + t.position + " rot " + t.eulerAngles + " scale " + t.lossyScale);
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) { sb.AppendLine(" -- MISSING"); continue; }
                if (c is Transform) continue;
                if (!(c is MonoBehaviour)) { string x = ""; if (c is Collider col) x = " trigger " + col.isTrigger + " en " + col.enabled + " b " + col.bounds; if (c is Renderer r) x = " mat " + (r.sharedMaterial ? r.sharedMaterial.name : "-") + " b " + r.bounds; sb.AppendLine(" -- " + c.GetType().Name + x); if (!(c is AudioSource)) continue; }
                else sb.AppendLine(" -- " + c.GetType().Name + (((MonoBehaviour)c).enabled ? "" : " (off)"));
                Props(new SerializedObject(c), sb);
            }
        }
        File.WriteAllText(Dir + "insp.txt", sb.ToString());
    }

    public static void DumpAsset(Object o)
    {
        var sb = new StringBuilder(); Props(new SerializedObject(o), sb);
        File.WriteAllText(Dir + "insp.txt", sb.ToString());
    }

    static void Props(SerializedObject so, StringBuilder sb)
    {
        var it = so.GetIterator(); bool e = true; int n = 0;
        while (it.NextVisible(e) && n++ < 900)
        {
            e = it.propertyType == SerializedPropertyType.Generic;
            string v = null;
            switch (it.propertyType)
            {
                case SerializedPropertyType.ObjectReference: if (it.objectReferenceValue) v = (it.objectReferenceValue is Component cc ? PathOf(cc.transform) : it.objectReferenceValue is GameObject gg ? PathOf(gg.transform) : it.objectReferenceValue.name) + " (" + it.objectReferenceValue.GetType().Name + ")"; break;
                case SerializedPropertyType.Boolean: v = it.boolValue.ToString(); break;
                case SerializedPropertyType.Float: v = it.floatValue.ToString("0.###"); break;
                case SerializedPropertyType.Integer: v = it.longValue.ToString(); break;
                case SerializedPropertyType.String: if (it.stringValue != "") v = it.stringValue; break;
                case SerializedPropertyType.Enum: v = it.enumValueIndex + ""; break;
                case SerializedPropertyType.Vector3: v = it.vector3Value.ToString(); break;
                case SerializedPropertyType.Vector2: v = it.vector2Value.ToString(); break;
            }
            string p = it.propertyPath;
            if (v == null || p == "m_Script" || p.Contains("AssemblyTypeName") || p.Contains("m_Mode") || p.Contains("m_CallState") || p.Contains("m_FallbackState") || p.Contains("LocalVariables") || p.Contains("WaitForCompletion") || p.Contains("m_FloatArgument") || p.Contains("m_IntArgument") || p.Contains("m_StringArgument")) continue;
            sb.AppendLine("   " + p + " = " + v);
        }
    }

    /// <summary>Who points at this object (or its components / children)?</summary>
    public static string Refs(string endsWith)
    {
        var target = Find(endsWith);
        if (target == null) return "not found";
        var sb = new StringBuilder(PathOf(target) + " <- ");
        foreach (var c in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c == null || c.transform.IsChildOf(target)) continue;
            var it = new SerializedObject(c).GetIterator(); bool e = true;
            while (it.Next(e))
            {
                e = it.propertyType == SerializedPropertyType.Generic;
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                Transform t = it.objectReferenceValue is GameObject g ? g.transform : it.objectReferenceValue is Component k ? k.transform : null;
                if (t != null && t == target) { string m = ""; if (it.propertyPath.EndsWith("m_Target")) { var q = it.Copy(); m = " ." + new SerializedObject(c).FindProperty(it.propertyPath.Replace("m_Target", "m_MethodName")).stringValue + " bool " + new SerializedObject(c).FindProperty(it.propertyPath.Replace("m_Target", "m_Arguments.m_BoolArgument")).boolValue; } sb.AppendLine().Append("     " + PathOf(c.transform) + " [" + c.GetType().Name + "] " + it.propertyPath + m); }
            }
        }
        return sb.ToString();
    }

    public static void SetStr(string table, string key, string en, string ar)
    {
        var col = UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection(table);
        var entry = col.SharedData.GetEntry(key) ?? col.SharedData.AddKey(key);
        foreach (var t in col.StringTables)
        {
            string text = t.LocaleIdentifier.Code.StartsWith("ar") ? ArabicSupport.ArabicFixer.Fix(ar, false, false) : en;
            var e = t.GetEntry(entry.Id);
            if (e == null) t.AddEntry(entry.Id, text); else e.Value = text;
            EditorUtility.SetDirty(t);
        }
        EditorUtility.SetDirty(col.SharedData);
    }

    public static string Str(string table, string key)
    {
        var col = UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection(table);
        var entry = col.SharedData.GetEntry(key);
        if (entry == null) return "(no key " + key + ")";
        var s = key + ":";
        foreach (var t in col.StringTables) { var e = t.GetEntry(entry.Id); s += " [" + t.LocaleIdentifier.Code + "] " + (e != null ? e.Value : "-"); }
        return s;
    }

    /// <summary>A picture from 'from' looking at 'at' (edit mode or Play), fog off optional.</summary>
    public static string Shot(string file, Vector3 from, Vector3 at, float fov = 60f, int w = 960, int h = 600, float far = 200f, bool ortho = false, float size = 10f)
    {
        var go = new GameObject("agent cam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.transform.position = from;
        cam.transform.rotation = Quaternion.LookRotation(at - from, Mathf.Abs(Vector3.Dot((at - from).normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up);
        cam.fieldOfView = fov; cam.farClipPlane = far; cam.nearClipPlane = 0.05f; cam.orthographic = ortho; cam.orthographicSize = size;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.45f, 0.4f, 0.33f);
        cam.cullingMask = ~(1 << 5);
        var rt = new RenderTexture(w, h, 24); cam.targetTexture = rt;
        bool fog = RenderSettings.fog; RenderSettings.fog = false;
        cam.Render();
        RenderSettings.fog = fog;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = null; cam.targetTexture = null;
        File.WriteAllBytes(Dir + file, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        return Dir + file;
    }
}
#endif
