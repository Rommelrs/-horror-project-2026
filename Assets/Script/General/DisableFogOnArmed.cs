using UnityEngine;

// Turns Enviro's fog post effect off for the rest of the scene once the player has the gun.
// Hook DisableFog() to the pistol pickup's OnInteracted event. It also checks the weapon state itself, so
// the fog stays off after dying and restoring a checkpoint taken after the pickup (the pickup event
// doesn't fire again in that case).
public class DisableFogOnArmed : MonoBehaviour
{
    bool fogDisabled;

    public void DisableFog()
    {
        fogDisabled = true;
        ApplyFogOff();
    }

    void Update()
    {
        if (fogDisabled)
        {
            // Enviro re-applies its own setting every frame, so keep ours on top
            ApplyFogOff();
            return;
        }

        Player player = Player.instance;
        if (player != null && player.playerWeaponSystem != null && player.playerWeaponSystem.weaponIsEnabled)
            DisableFog();
    }

    static void ApplyFogOff()
    {
        if (EnviroSkyLite.instance != null)
            EnviroSkyLite.instance.usePostEffectFog = false;
    }
}
