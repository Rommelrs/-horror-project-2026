using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Place this on any persistent GameObject in the GAME SCENE.
/// It restores the checkpoint data when the scene loads.
/// </summary>
public class CheckpointRestorer : MonoBehaviour
{
    private IEnumerator Start()
    {
        // Only restore if there is pending checkpoint data
        if (CheckpointManager.pendingRestoreData == null)
            yield break;

        CheckpointData data = CheckpointManager.pendingRestoreData;
        CheckpointManager.pendingRestoreData = null;

        // Stop all Timeline directors so cutscenes don't play
        foreach (PlayableDirector d in FindObjectsOfType<PlayableDirector>())
            d.Stop();

        // Wait for scene to fully initialize
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        Player p = Player.instance;
        if (p == null) yield break;

        // ─ Position
        p.controller.enabled = false;
        yield return new WaitForEndOfFrame();
        p.transform.position = new Vector3(data.posX, data.posY, data.posZ);
        p.transform.rotation = Quaternion.Euler(0, data.rotY, 0);
        yield return new WaitForEndOfFrame();
        p.controller.enabled = true;

        // ─ Health
        if (p.health != null)
        {
            p.health.ResetHealth();
            int dmg = p.health.GetMaxHealthValue() - data.health;
            if (dmg > 0) p.health.Damage(dmg);
        }

        // ─ Stability
        if (p.playerStability != null)
            p.playerStability.stability = data.stability;

        // ─ Weapon
        if (p.playerWeaponSystem != null)
        {
            p.playerWeaponSystem.weaponIsEnabled = data.weaponEnabled;
            p.playerWeaponSystem.currentAmmo = data.currentAmmo;
        }

        // ─ Maps
        p.hasMap = data.hasMap;
        if (MapHandler.instance != null)
        {
            if (data.hasMap1) MapHandler.instance.UnlockMap1();
            if (data.hasMap2) MapHandler.instance.UnlockMap2();
        }

        // ─ Inventory
        if (p.inventory != null)
        {
            p.inventory.ClearInventory();
            yield return new WaitForEndOfFrame();

            foreach (var entry in data.items)
            {
                Item item = FindItem(entry.itemName);
                if (item != null) p.inventory.AddItem(item, entry.quantity);
            }
        foreach (var entry in data.notes)
        {
            Item note = FindItem(entry.itemName);
            if (note != null) p.inventory.AddItem(note, entry.quantity);
        }

        // Mark inventory as initialized so it doesn't reset on next scene load
        p.inventory.MarkInitialized();
    }

    }

    Item FindItem(string itemName)
    {
        Item[] all = Resources.LoadAll<Item>("Items");
        foreach (var item in all)
            if (item.itemName == itemName || item.name == itemName)
                return item;
        return null;
    }
}
