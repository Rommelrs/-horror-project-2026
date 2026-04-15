using System.Collections;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerWeaponEnabler : MonoBehaviour
{
    //[SerializeField] GameObject weaponUIPanel;
    [SerializeField] GameObject weaponObj;

    PlayerWeaponSystem playerWeaponSystem;
    Inventory inventory;

    private void Awake()
    {
        inventory = GetComponent<Inventory>();
        playerWeaponSystem = GetComponent<PlayerWeaponSystem>();
    }

    private IEnumerator Start()
    {
        if (inventory != null)
            inventory.OnInventoryItemUpdated.AddListener(CheckIfPlayerHasWeapon);

        // Wait a frame for inventory to initialize, then check weapon state
        yield return null;
        CheckIfPlayerHasWeapon();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryItemUpdated.RemoveListener(CheckIfPlayerHasWeapon);
    }

    void CheckIfPlayerHasWeapon()
    {
        if(inventory != null && inventory.HasWeapon())
        {
            //Enable Weapon
            playerWeaponSystem.weaponIsEnabled = true;

            //weaponUIPanel.SetActive(false);

            if(!playerWeaponSystem.isAiming)
                weaponObj.SetActive(true);
        }
        else
        {
            //Disable Weapon
            playerWeaponSystem.weaponIsEnabled = false;
            //weaponUIPanel.SetActive(false);
            weaponObj.SetActive(false);
        }
    }
}
