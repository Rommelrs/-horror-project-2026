using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraShaker : MonoBehaviour
{
    [SerializeField] float smooth = 1f;
    [SerializeField] float smoothRot = 1f;

    bool isShaking = false;
    Vector3 shakeOffsetPos;
    Vector3 shakeOffsetRot;

    Vector3 defaultPosition;

    private void Awake()
    {
        defaultPosition = transform.localPosition;
    }

    void Update()
    {
        CompositePositionRotation();
    }

    //Update local position and rotation
    void CompositePositionRotation()
    {
        transform.localPosition = Vector3.Lerp(transform.localPosition, defaultPosition + shakeOffsetPos, Time.deltaTime * smooth);

        Quaternion targetRot = Quaternion.Euler(shakeOffsetRot);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * smoothRot);
    }

    //Apply short shake to the object
    public void ApplyShake(float shakeIntensity = 1f, float shakeDuration = 0.15f)
    {
        if (!isShaking)
            StartCoroutine(ShakeRoutine(shakeIntensity, shakeDuration));
    }

    IEnumerator ShakeRoutine(float intensity, float shakeDuration)
    {
        isShaking = true;
        float timeElapsed = 0f;

        while (timeElapsed < shakeDuration)
        {
            float strength = Mathf.Lerp(intensity, 0f, timeElapsed / shakeDuration);

            shakeOffsetPos = Random.insideUnitSphere * 0.02f * strength;
            shakeOffsetRot = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ) * 1.5f * strength;

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        shakeOffsetPos = Vector3.zero;
        shakeOffsetRot = Vector3.zero;
        isShaking = false;
    }
}
