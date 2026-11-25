using UnityEngine;
using UnityEngine.UI;

/// <summary>A copy of one of the on-screen key prompts (GameCanvas/...PromptUI) with its own words: same look, same key picture.</summary>
public static class PromptCopy
{
    public static InteractPromptUI Make(string prefix, string source, string suffix, float lift = 0f)
    {
        InteractPromptUI from = null;
        foreach (var p in Object.FindObjectsByType<InteractPromptUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (p.name == source) { from = p; break; }
        if (from == null) return null;
        var go = Object.Instantiate(from.gameObject, from.transform.parent);
        go.name = source + " (copy)";
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition += new Vector2(0f, lift);
        Set(rt, "PrefixText", prefix);
        Set(rt, "SuffixText", suffix);
        go.SetActive(true);       // its Awake hides it again
        return go.GetComponent<InteractPromptUI>();
    }

    static void Set(RectTransform rt, string child, string words)
    {
        if (words == null) return;
        var t = rt.Find(child);
        if (t == null || !t.TryGetComponent<Text>(out var text)) return;
        text.text = words;
        // the row lays its pieces out by their own widths
        var r = (RectTransform)t;
        r.sizeDelta = new Vector2(Mathf.Ceil(text.preferredWidth) + 4f, r.sizeDelta.y);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
    }
}
