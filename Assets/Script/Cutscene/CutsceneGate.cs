using UnityEngine;

/// <summary>
/// One answer to "is the player out of control right now?" for the things a cutscene must never allow: switching weapons, aiming,
/// opening the map, using a door. New cutscenes only have to show up here (or switch DoorInteractionHandler off, as most do).
/// </summary>
public static class CutsceneGate
{
    /// <summary>A cutscene holds him.</summary>
    public static bool Playing =>
        DoorInteractionHandler.WorldInteractionBlocked
        || AdelDoorSequence.IsPlaying || AdelPoleSequence.IsPlaying || BridgeRunnerSequence.IsPlaying
        || CowardMeetingCutscene.IsPlaying || KidnapSequence.IsPlaying || StorageMeetingCutscene.IsPlaying
        || CinematicPreSequenceController.Running || CinematicKnockdownSequence.Scripted
        || AutoInteractCutscene.Running || AutoPickupCutscene.Running;

    /// <summary>A scripted chase (he runs, no weapon, no map).</summary>
    public static bool Chasing => KidnapSequence.ChaseActive || BridgeRunnerSequence.IsPlaying;

    /// <summary>No map screen: cutscenes and chases.</summary>
    public static bool BlocksMap => Playing || Chasing || GrabCounterTutorial.Holding || StabilityTutorial.Open || DeathHelp.Open;
    /// <summary>No weapon switching: cutscenes, chases, and the gun tutorial until the first shot.</summary>
    public static bool BlocksWeaponSwitch => Playing || Chasing || GunTutorialPrompts.LocksWeapon || GrabCounterTutorial.Holding;
    /// <summary>No raising a weapon by the aim button (a cutscene may still aim for him).</summary>
    public static bool BlocksAim => Playing;
}
