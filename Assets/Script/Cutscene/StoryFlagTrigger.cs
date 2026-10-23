using UnityEngine;

// Sets a story flag the first time the player walks into this object's trigger collider (if it has one), or whenever
// SetFlag() is called from an event (e.g. the Alley 2 entrance doors' OnInteractedSuccess sets "Alley2Visited").
public class StoryFlagTrigger : MonoBehaviour
{
    [SerializeField] string flag = "Alley2Visited";

    public void SetFlag()
    {
        if (!StoryFlags.Has(flag)) StoryFlags.Set(flag);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) SetFlag();
    }
}
