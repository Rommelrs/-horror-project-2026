using UnityEngine;

public class PersistentUI : MonoBehaviour
{
    public static PersistentUI instance;

    private void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("PersistentUI: Setting as singleton and marking DontDestroyOnLoad");
        }
        else if (instance != this)
        {
            Debug.Log("PersistentUI: Duplicate detected, destroying: " + gameObject.name);
            Destroy(gameObject);
        }
    }
}
