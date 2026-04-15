using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerScared : MonoBehaviour
{
    public static PlayerScared instance;

    public AudioClip []scaredSFX;
    public float scaredDuration = 3f;

    bool scared = false;
    AudioSource audioSource;

    private void Awake()
    {
        instance = this;
        audioSource = GetComponent<AudioSource>();
    }

    public void TriggerPlayerScaredBehaviour()
    {
        if (!scared)
        {
            scared = true;
            Player.instance.isScared = true;

            //Trigger Scared Behaviour
            StartCoroutine(Co_TriggerScaredBehaviour());
        }
    }

    IEnumerator Co_TriggerScaredBehaviour()
    {
        //Play Scared SFX
        if (scaredSFX != null && scaredSFX.Length > 0)
        {
            audioSource.PlayOneShot(scaredSFX[Random.Range(0, scaredSFX.Length)]);
        }

        //Trigger Scared Animation
        Player.instance.animator.SetTrigger("Scared");

        yield return new WaitForSeconds(scaredDuration);

        //Reset scared bool
        scared = false;

        Player.instance.isScared = false;
    }
}
