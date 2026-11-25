using UnityEditor;
using UnityEngine;

public static partial class Nov25Builder
{
    // ------------------------------------------------------------------ 28: dust on every pickup

    public static string Dust()
    {
        var source = AgentTools.Find("Knife Scenario/KnifeHandlePickup/DustEffect").gameObject;
        string s = "";
        foreach (var p in Object.FindObjectsByType<InspectableItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p.itemToPickup == null || p.GetComponentInChildren<ParticleSystem>(true) != null) continue;
            var d = Object.Instantiate(source, p.transform);
            d.name = "DustEffect";
            d.transform.SetPositionAndRotation(p.transform.position + Vector3.up * 0.06f, source.transform.rotation);
            d.transform.localScale = source.transform.localScale;
            d.SetActive(true);
            Dirty(p.gameObject);
            s += p.name + " ";
        }
        // the circuit box key's dust was a third of the others' size, on a dark car hood
        var key = AgentTools.Find("CircuitBoxKeyPickup");
        if (key != null)
        {
            var ps = key.GetComponentInChildren<ParticleSystem>(true);
            ps.transform.localScale = source.transform.localScale;
            ps.transform.position = key.position + Vector3.up * 0.12f;
            Dirty(key.gameObject);
            s += "| circuit key dust " + ps.transform.localScale;
        }
        return s;
    }

    // ------------------------------------------------------------------ 29: "I have to find the kid first" goes by itself after 2 s

    public static void KidLine()
    {
        var t = AgentTools.Find("Boundaries/Cube (5)").GetComponent<SubtitleTrigger>();
        var so = new SerializedObject(t);
        so.FindProperty("spokenLine").boolValue = true;
        so.FindProperty("lifeTime").floatValue = 2f;
        so.FindProperty("freezeGame").boolValue = false;
        so.FindProperty("waitForInteract").boolValue = false;
        so.ApplyModifiedProperties();
        Dirty(t.gameObject);
    }
}

public static partial class Nov25Builder
{
    // ------------------------------------------------------------------ 9 + 44: map markers

    public static string MapMarkers(float bridgeLeft = 34f)
    {
        var circle = (RectTransform)AgentTools.Find("Street_Markers/BridgeCircle");
        circle.anchoredPosition = new Vector2(BridgeCircleX(circle) - bridgeLeft, circle.anchoredPosition.y);
        EditorUtility.SetDirty(circle);

        var marker = AgentTools.Find("Street_Markers/KnifeBladeMarker");
        var template = AgentTools.Find("MapMarkers/PreMarked/Success/Success");
        var street = marker.parent;
        var old = street.Find("KnifeBladeCheck");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var check = Object.Instantiate(template.gameObject, street);
        check.name = "KnifeBladeCheck";
        var rt = (RectTransform)check.transform;
        rt.position = marker.position;
        rt.anchoredPosition += new Vector2(46f, 40f);
        rt.localScale = Vector3.one * 0.55f;
        check.SetActive(false);
        var comp = street.gameObject.GetComponent<MapItemCheck>() ?? street.gameObject.AddComponent<MapItemCheck>();
        var so = new SerializedObject(comp);
        so.FindProperty("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Item>("Assets/Resources/Items/Key items/Knife.asset");
        so.FindProperty("marker").objectReferenceValue = marker.gameObject;
        so.FindProperty("check").objectReferenceValue = check;
        so.ApplyModifiedProperties();
        Dirty(marker.gameObject);
        return "circle " + circle.anchoredPosition + " check " + rt.anchoredPosition;
    }

    const string CircleXKey = "Nov25.BridgeCircleX";
    static float BridgeCircleX(RectTransform circle)
    {
        // the original spot, remembered so running this again doesn't walk the marker across the map
        if (!EditorPrefs.HasKey(CircleXKey)) EditorPrefs.SetFloat(CircleXKey, circle.anchoredPosition.x);
        return EditorPrefs.GetFloat(CircleXKey);
    }
}
