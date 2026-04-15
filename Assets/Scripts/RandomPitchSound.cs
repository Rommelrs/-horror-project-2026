using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RandomPitchSound : MonoBehaviour
{
    public float minInterval = 2f;
    public float maxInterval = 6f;
    public float minPitch = 0.8f;
    public float maxPitch = 1.2f;

    private AudioSource audioSource;
    private float timer;
    private float currentInterval;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        currentInterval = Random.Range(minInterval, maxInterval);
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= currentInterval)
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.Play();
            timer = 0f;
            currentInterval = Random.Range(minInterval, maxInterval);
        }
    }
}
