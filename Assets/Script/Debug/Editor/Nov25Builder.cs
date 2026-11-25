using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Timeline;
using UIImage = UnityEngine.UI.Image;

/// <summary>Scene / asset edits of the 2026-11-25 playtest round. Every step is safe to run again.</summary>
public static partial class Nov25Builder
{
    const string Game = "Assets/Scenes/Game.unity";

    static void Dirty(GameObject g) { EditorSceneManager.MarkSceneDirty(g.scene); }

    // ------------------------------------------------------------------ 52 + 53: the combination lock

    public static void CombinationLock()
    {
        string dir = "Assets/Sprites/UI"; Directory.CreateDirectory(dir);
        string p = dir + "/DialArrow.png";
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = y / (float)(n - 1);
                float half = (1f - v) * 0.46f;
                float d = half - Mathf.Abs(x / (float)(n - 1) - 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(d * n * 0.9f) * Mathf.Clamp01((v - 0.06f) * n * 0.9f)));
            }
        tex.Apply();
        File.WriteAllBytes(p, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(p);
        var ti = (TextureImporter)AssetImporter.GetAtPath(p);
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport();
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
        for (int i = 0; i < 4; i++)
            foreach (var d in new[] { "Up", "Down" })
            {
                var b = AgentTools.Find("GameCanvas/Menus/CombinationLock/Dial " + i + "/" + d);
                var old = b.Find("Arrow");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var go = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIImage)) { layer = 5 };
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(b, false);
                rt.sizeDelta = new Vector2(34f, 22f);
                // the buttons are squashed: the arrow keeps the panel's own scale, and sits just off the dial's edge
                Vector3 panel = AgentTools.Find("GameCanvas/Menus/CombinationLock").lossyScale, bs = b.lossyScale;
                rt.localScale = new Vector3(panel.x / bs.x, panel.y / bs.y, 1f);
                var dial = (RectTransform)b.parent;
                var corners = new Vector3[4]; dial.GetWorldCorners(corners);
                float edge = d == "Up" ? corners[1].y : corners[0].y;
                rt.position = new Vector3((corners[0].x + corners[2].x) * 0.5f, edge + (d == "Up" ? 1f : -1f) * 18f * panel.y, rt.position.z);
                rt.eulerAngles = new Vector3(0f, 0f, d == "Up" ? 0f : 180f);
                var im = go.GetComponent<UIImage>();
                im.sprite = sp; im.color = new Color(0.95f, 0.88f, 0.62f, 0.9f); im.raycastTarget = false;
            }
        AgentTools.SetStr("Subtitles", "Maybe this drawing on the wall could help", "Maybe this drawing on the wall could help", "يمكن هالرسمة على الجدار تساعد");
        AgentTools.SetStr("Subtitles", "Maybe I should focus on the removed numbers", "Maybe I should focus on the removed numbers", "يمكن لازم أركز على الأرقام الممسوحة");
        AgentTools.SetStr("Subtitles", "I should check the map", "I should check the map", "لازم أشوف الخريطة");
        var cl = AgentTools.Find("CombinationLock/CombinationLockInteractable").GetComponent<CombinationLockInteractable>();
        var so = new SerializedObject(cl);
        so.FindProperty("drawing").objectReferenceValue = AgentTools.Find("Family Lived Happily/Quad");
        so.FindProperty("drawingLine.m_TableReference.m_TableCollectionName").stringValue = "Subtitles";
        so.FindProperty("drawingLine.m_TableEntryReference.m_Key").stringValue = "Maybe this drawing on the wall could help";
        so.ApplyModifiedProperties();
        Dirty(cl.gameObject);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ 22: the two lines of the after-swarm cutscene, both versions

    public static string DreamLines()
    {
        string report = "";
        const string original = "AnimationTools/escape_coward/SadCutsceneTimeline.original.playable", tmp = "Assets/Cutscenes/_tmp_original.playable";
        File.Copy(original, tmp, true);
        AssetDatabase.ImportAsset(tmp, ImportAssetOptions.ForceUpdate);
        double originalLength = AssetDatabase.LoadAssetAtPath<TimelineAsset>(tmp).duration;
        AssetDatabase.DeleteAsset(tmp);

        var col = LocalizationEditorSettings.GetStringTableCollection("Subtitles");
        LocalizedString Line(string key) => new LocalizedString(col.TableCollectionName, col.SharedData.GetEntry(key).Id);
        const string Voice = "Assets/Audio/Cutscenes/ActionSequenceVoice/";
        foreach (var path in new[] { "Assets/Cutscenes/SadCutsceneTimeline.playable", "Assets/Cutscenes/SadCutsceneTimeline_B.playable" })
        {
            var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (tl == null) { report += path + " missing\n"; continue; }
            var track = tl.GetOutputTracks().OfType<AudioTrack>().FirstOrDefault(t => t.GetClips().Any(c => c.displayName == "ESC_K1"));
            if (track == null) { report += path + ": no ESC_K1 track\n"; continue; }
            foreach (var c in track.GetClips().Where(c => c.displayName == "ESC_DREAM" || c.displayName == "ESC_CANTSTOP").ToList()) track.DeleteClip(c);
            foreach (var m in track.GetMarkers().OfType<SubtitleMarker>().Where(m => m.lineId == "ESC_DREAM" || m.lineId == "ESC_CANTSTOP").ToList()) track.DeleteMarker(m);
            double grown = tl.duration - originalLength;

            void Say(string id, string file, double at, params (string key, double off, float until)[] subs)
            {
                var clip = track.CreateClip(AssetDatabase.LoadAssetAtPath<AudioClip>(Voice + file));
                clip.start = at; clip.displayName = id;
                foreach (var s in subs)
                {
                    var mk = track.CreateMarker<SubtitleMarker>(at + s.off + 0.05);
                    mk.line = Line(s.key); mk.duration = s.until; mk.lineId = id;
                }
            }
            var k1 = track.GetClips().First(c => c.displayName == "ESC_K1");
            var k2 = track.GetClips().First(c => c.displayName == "ESC_K2");
            double dream = k1.end + 0.6;                          // "This isn't normal..." - then this, before "What the hell did I walk into?"
            Say("ESC_DREAM", "VoiceLine3.mp3", dream, ("Am I in a dream or something?", 0, 2.3f));
            // "No... I can't stop. Not right now." over the moving shot, where it was, after the coward has gone
            double no = 59.3667 + grown;
            Say("ESC_CANTSTOP", "VoiceLine10.mp3", no, ("No...", 0, 1.9f), ("I can\u2019t stop", 2.53, 2.0f), ("Not right now.", 4.65, 1.8f));
            EditorUtility.SetDirty(track); EditorUtility.SetDirty(tl);
            report += path + ": dream " + dream.ToString("F2") + " (next line at " + k2.start.ToString("F2") + "), can't stop " + no.ToString("F2") + ", length " + tl.duration.ToString("F1") + " was " + originalLength.ToString("F1") + "\n   ";
            foreach (var c in track.GetClips().OrderBy(c => c.start)) report += c.displayName + " " + c.start.ToString("F1") + "-" + c.end.ToString("F1") + "  ";
            report += "\n";
        }
        AssetDatabase.SaveAssets();
        return report;
    }
}
