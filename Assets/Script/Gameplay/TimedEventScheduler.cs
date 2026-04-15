using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TimedEventScheduler : MonoBehaviour
{
    [System.Serializable]
    public class TimedEvent
    {
        public string label;
        public float delay;
        public UnityEvent onEvent;
    }

    [SerializeField] bool startOnAwake = true;
    [SerializeField] TimedEvent[] timedEvents;

    Coroutine schedulerCR;

    private void Start()
    {
        if (startOnAwake)
            StartScheduler();
    }

    public void StartScheduler()
    {
        if (schedulerCR != null) StopCoroutine(schedulerCR);
        schedulerCR = StartCoroutine(Co_RunScheduler());
    }

    public void StopScheduler()
    {
        if (schedulerCR != null) StopCoroutine(schedulerCR);
    }

    IEnumerator Co_RunScheduler()
    {
        foreach (var timedEvent in timedEvents)
        {
            yield return new WaitForSeconds(timedEvent.delay);
            timedEvent.onEvent?.Invoke();
        }
    }
}
