using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the game's version (Project Settings > Player > Version) in a menu corner, e.g. "v0.1.0".
/// Change the number there only - every label follows it. Works on a Text or a TextMeshPro label.
/// </summary>
public class VersionLabel : MonoBehaviour
{
    [SerializeField] string prefix = "v";

    void Awake()
    {
        string s = prefix + Application.version;
        var text = GetComponent<Text>();
        if (text != null) { text.text = s; return; }
        var tmp = GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = s;
    }
}
