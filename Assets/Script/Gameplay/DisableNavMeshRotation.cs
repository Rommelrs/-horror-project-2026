using UnityEngine;
using UnityEngine.AI;

public class DisableNavMeshRotation : MonoBehaviour
{
    NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.updateRotation = false;
        }
    }
}
