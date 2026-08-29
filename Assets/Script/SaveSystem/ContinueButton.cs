using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to the Continue button in the main menu.
/// Automatically shows/hides based on whether a checkpoint exists.
/// </summary>
public class ContinueButton : MonoBehaviour
{
    [Tooltip("Optional: text to show the checkpoint scene name.")]
    [SerializeField] TMP_Text checkpointInfoText;

    static string CheckpointFilePath => Path.Combine(Application.persistentDataPath, "Saves", "checkpoint.json");

    private void Start()
    {
        Refresh();
    }

    /// <summary>Call this to manually refresh the button state.</summary>
    public void Refresh()
    {
        // Check file directly - works even if CheckpointManager isn't loaded yet
        bool hasCheckpoint = File.Exists(CheckpointFilePath);
        gameObject.SetActive(hasCheckpoint);

        if (hasCheckpoint && checkpointInfoText != null && CheckpointManager.instance != null)
        {
            string scene = CheckpointManager.instance.GetCheckpointSceneName();
            checkpointInfoText.text = scene;
        }
    }

    /// <summary>Wire this to the button's OnClick event.</summary>
    public void OnContinueClicked()
    {
        if (CheckpointManager.instance != null)
            CheckpointManager.instance.ContinueFromCheckpoint();
    }
}
