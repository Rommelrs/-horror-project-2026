using UnityEngine;

/// <summary>
/// A map marker that gets its check mark once Khalid owns an item (the knife blade: checked when he has the Knife). Asked every time
/// the map opens (MapHandler.EnableMapMenu), so it follows the inventory through saves and checkpoints.
/// </summary>
public class MapItemCheck : MonoBehaviour
{
    [SerializeField] Item item;
    [Tooltip("The check is only drawn while this marker is on the map.")]
    [SerializeField] GameObject marker;
    [SerializeField] GameObject check;
    [Tooltip("Story flag set the first time he owns it (the item may be used up later).")]
    [SerializeField] string flag = "KnifeOwned";

    public static void RefreshAll()
    {
        foreach (var c in FindObjectsByType<MapItemCheck>(FindObjectsInactive.Include, FindObjectsSortMode.None)) c.Refresh();
    }

    public void Refresh()
    {
        var p = Player.instance;
        bool owns = p != null && p.inventory != null && item != null && p.inventory.HasItem(item);
        if (owns && !StoryFlags.Has(flag)) StoryFlags.Set(flag);
        if (check != null) check.SetActive((owns || StoryFlags.Has(flag)) && (marker == null || marker.activeSelf));
    }
}
