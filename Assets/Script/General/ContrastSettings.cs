using UnityEngine;

/// <summary>
/// Project-wide setting for the Custom/CustomUnlit contrast & brightness boost.
/// Edit the asset at Assets/Resources/ContrastSettings.asset.
/// </summary>
public class ContrastSettings : ScriptableObject
{
    [Tooltip("The contrast / brightness sliders only show for cameras that render at least one of these layers " +
             "(Default only, by default). Cameras that don't - such as the Inspection camera, which only renders the " +
             "Inspection layer, or the Map camera - show the item with neutral contrast and brightness.")]
    public LayerMask contrastLayers = 1; // Default
}
