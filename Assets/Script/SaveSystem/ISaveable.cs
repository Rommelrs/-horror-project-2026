/// <summary>
/// Interface for objects that can save and load their state
/// Implement this on any MonoBehaviour that needs to persist data
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// Save the object's current state to the save data
    /// </summary>
    void Save(SaveData saveData);
    
    /// <summary>
    /// Load and apply the saved state to the object
    /// </summary>
    void Load(SaveData saveData);
}
