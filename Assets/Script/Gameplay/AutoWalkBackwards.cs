using System.Collections;
using UnityEngine;

/// <summary>
/// Makes the player walk backwards (without turning) for a set distance, then hands control back.
/// Hook WalkBack() up to SubtitleTrigger's "On Subtitle Trigger Completed" event.
/// </summary>
public class AutoWalkBackwards : MonoBehaviour
{
    [SerializeField] float distance = 3f;
    [SerializeField] float speed = 1.5f;

    Coroutine walkCR;

    public void WalkBack()
    {
        if (walkCR != null || Player.instance == null)
            return;

        walkCR = StartCoroutine(Co_WalkBack());
    }

    IEnumerator Co_WalkBack()
    {
        Player player = Player.instance;

        player.playerWeaponSystem.ExitOutOfAiming();
        player.pauseMovement = true;

        Vector3 direction = -player.transform.forward;
        direction.y = 0f;
        direction.Normalize();

        float walked = 0f;
        while (walked < distance)
        {
            float step = speed * Time.deltaTime;
            Vector3 move = direction * step;
            move.y = -9.81f * Time.deltaTime;
            player.controller.Move(move);
            walked += step;

            player.animator.SetFloat("Velocity", speed);
            player.animator.SetFloat("x", 0f);
            player.animator.SetFloat("y", -1f);

            yield return null;
        }

        player.animator.SetFloat("Velocity", 0f);
        player.animator.SetFloat("y", 0f);
        player.pauseMovement = false;
        walkCR = null;
    }
}
