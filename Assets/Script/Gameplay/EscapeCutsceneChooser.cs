using UnityEngine;

/// <summary>
/// Block 2 swarm escape: the cinder-block door calls <see cref="Play"/> instead of switching the cutscene on itself.
/// Block 3 cloth already cut -> version A (Khalid walks down the lane into Block 3 and rests there, as before).
/// Not cut yet (he went for the duct tape first) -> version B: same cutscene and lines, but he walks out on the street
/// and rests against the wall just outside the Block 3 cloth, and the game carries on from there.
/// </summary>
public class EscapeCutsceneChooser : MonoBehaviour
{
    [Tooltip("The Block 3 cloth (Alley 3 Gameplay/ClothBlockage).")]
    [SerializeField] BurnableObstacle block3Cloth;
    [SerializeField] GameObject versionA;
    [SerializeField] GameObject versionB;

    public static bool Block3ClothCut(BurnableObstacle cloth) => cloth == null || !cloth.CanUseLighter();

    public void Play()
    {
        bool cut = Block3ClothCut(block3Cloth);
        var go = cut || versionB == null ? versionA : versionB;
        if (go != null) go.SetActive(true);
    }
}
