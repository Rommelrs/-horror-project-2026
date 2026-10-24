using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Turns Khalid clips exported from Blender (AnimationTools, export_action -> Assets/_TempKhalidClips/<name>.fbx) into
// .anim files: imported like MainCharacterAnimated.fbx, the helper nodes (char_grp, the export dummy) dropped and the three
// top nodes (rig, c_pos, c_traj) held at the values Khalid's own idle uses, so the clips line up with his other clips.
public static class KhalidClipImporter
{
    public const string TempDir = "Assets/_TempKhalidClips";
    static readonly string[] TopNodes = { "char_grp/rig", "char_grp/rig/c_pos", "char_grp/rig/c_pos/c_traj" };

    // every FBX in TempDir -> outDir/<name>.anim (loopNames loop); the temp folder is removed afterwards
    public static string ImportAll(string outDir, params string[] loopNames)
    {
        var main = (ModelImporter)AssetImporter.GetAtPath("Assets/MainCharacter/MainCharacterAnimated.fbx");
        var idle = AssetDatabase.LoadAllAssetsAtPath(main.assetPath).OfType<AnimationClip>().First(c => c.name == "rig|khalid_idle");
        var idleBindings = AnimationUtility.GetCurveBindings(idle);
        AssetDatabase.Refresh();
        var log = "";
        foreach (var fbx in Directory.GetFiles(TempDir, "*.fbx"))
        {
            string src = fbx.Replace('\\', '/'), name = Path.GetFileNameWithoutExtension(src);
            var mi = (ModelImporter)AssetImporter.GetAtPath(src);
            mi.animationType = main.animationType; mi.globalScale = main.globalScale; mi.useFileScale = main.useFileScale;
            mi.bakeAxisConversion = main.bakeAxisConversion; mi.resampleCurves = main.resampleCurves;
            mi.animationCompression = ModelImporterAnimationCompression.Off;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.SaveAndReimport();
            var take = AssetDatabase.LoadAllAssetsAtPath(src).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));

            string outPath = outDir + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip { name = name };
            clip.ClearCurves();
            clip.frameRate = take.frameRate;
            foreach (var b in AnimationUtility.GetCurveBindings(take))
            {
                if (b.path == "char_grp" || b.path.StartsWith("zz_")) continue;
                AnimationCurve curve;
                if (TopNodes.Contains(b.path))
                {
                    var ib = idleBindings.First(x => x.path == b.path && x.propertyName == b.propertyName);
                    curve = AnimationCurve.Constant(0f, take.length, AnimationUtility.GetEditorCurve(idle, ib).Evaluate(0f));
                }
                else curve = AnimationUtility.GetEditorCurve(take, b);
                AnimationUtility.SetEditorCurve(clip, b, curve);
            }
            // the Blender actions leave the head control unkeyed (identity) where his idle has it tipped ~29 deg down: a
            // clip that starts or ends on the idle (the gun raise / lower) eases it from / to the idle's value there
            if (name.Contains("Raise")) BlendHeadControl(clip, idle, atStart: true);
            if (name.Contains("Lower")) BlendHeadControl(clip, idle, atStart: false);
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = loopNames.Contains(name);
            AnimationUtility.SetAnimationClipSettings(clip, st);
            if (isNew) AssetDatabase.CreateAsset(clip, outPath); else EditorUtility.SetDirty(clip);
            log += name + " " + take.length.ToString("F2") + "s; ";
        }
        AssetDatabase.SaveAssets();
        FileUtil.DeleteFileOrDirectory(TempDir); FileUtil.DeleteFileOrDirectory(TempDir + ".meta");
        AssetDatabase.Refresh();
        return log;
    }

    const string HeadControl = "char_grp/rig/c_pos/c_traj/head_scale_fix.x/c_head.x";

    // c_head.x eased (smoothstep, slerped per frame) between the idle's value and the clip's own, over the whole clip:
    // from the idle at the start (atStart) or to it at the end
    public static void BlendHeadControl(AnimationClip clip, AnimationClip idle, bool atStart)
    {
        string path = AnimationUtility.GetCurveBindings(clip).Select(b => b.path).FirstOrDefault(p => p.EndsWith("/c_head.x")) ?? HeadControl;
        var axes = new[] { "x", "y", "z", "w" };
        EditorCurveBinding B(string axis) => EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + axis);
        Quaternion Read(AnimationClip c, float t)
        {
            var v = new float[4];
            for (int i = 0; i < 4; i++) { var curve = AnimationUtility.GetEditorCurve(c, B(axes[i])); v[i] = curve != null ? curve.Evaluate(t) : (i == 3 ? 1f : 0f); }
            return new Quaternion(v[0], v[1], v[2], v[3]).normalized;
        }
        Quaternion idleRot = Read(idle, 0f);
        int n = Mathf.Max(1, Mathf.RoundToInt(clip.length * 30f));
        var keys = new Keyframe[4][];
        for (int i = 0; i < 4; i++) keys[i] = new Keyframe[n + 1];
        for (int f = 0; f <= n; f++)
        {
            float t = clip.length * f / n, u = Mathf.SmoothStep(0f, 1f, (float)f / n);
            Quaternion own = Read(clip, t);
            Quaternion q = atStart ? Quaternion.Slerp(idleRot, own, u) : Quaternion.Slerp(own, idleRot, u);
            for (int i = 0; i < 4; i++) keys[i][f] = new Keyframe(t, q[i]);
        }
        for (int i = 0; i < 4; i++) AnimationUtility.SetEditorCurve(clip, B(axes[i]), new AnimationCurve(keys[i]));
        EditorUtility.SetDirty(clip);
    }
}
