using System.Collections;
using UnityEngine;

/// <summary>
/// When the player closes the inspection of one item (the Fuse), a note is added to their Files and opened straight away
/// (Samir's credit book, which was lying under the fuse in the drawer). Happens once; the save remembers it.
/// </summary>
public class AutoInspectNoteAfter : MonoBehaviour
{
    [Tooltip("Item name whose inspection closing starts this (e.g. 'Fuse').")]
    [SerializeField] string afterItemName = "Fuse";
    [SerializeField] Item noteToShow;
    [Tooltip("Pause between the first inspection closing and the note opening.")]
    [SerializeField] float delay = 0.35f;
    [Tooltip("Optional prop that stands for the note in the world (the book in the drawer); hidden once the note is taken.")]
    [SerializeField] GameObject worldProp;
    [Tooltip("Saved in the used-interactables list so a reload doesn't show it twice.")]
    [SerializeField] string saveKey = "store:credit-book";

    bool done;

    void Start()
    {
        if (SaveManager.instance != null && SaveManager.instance.IsInteractableUsed(saveKey))
        {
            done = true;
            if (worldProp != null) worldProp.SetActive(false);
        }
        if (ItemInspectionHandler.instance != null)
            ItemInspectionHandler.instance.onCloseInspection += OnInspectionClosed;
    }

    void OnDestroy()
    {
        if (ItemInspectionHandler.instance != null)
            ItemInspectionHandler.instance.onCloseInspection -= OnInspectionClosed;
    }

    void OnInspectionClosed(string itemName)
    {
        if (done || noteToShow == null || itemName != afterItemName) return;
        done = true;
        if (SaveManager.instance != null) SaveManager.instance.RegisterUsedInteractable(saveKey);
        StartCoroutine(Co_Show());
    }

    IEnumerator Co_Show()
    {
        yield return new WaitForSecondsRealtime(delay);
        if (worldProp != null) worldProp.SetActive(false);
        if (Player.instance != null && Player.instance.inventory != null)
            Player.instance.inventory.AddItem(noteToShow, 1);
        if (ItemInspectionHandler.instance != null)
            ItemInspectionHandler.instance.InspectItem(noteToShow);
        Debug.Log("[AutoInspectNoteAfter] '" + noteToShow.itemName + "' opened after '" + afterItemName + "'");
    }
}
