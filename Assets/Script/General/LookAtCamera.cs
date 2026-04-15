using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    Camera mainCamera;

    private void Start()
    {
        //Get Main Camera
        mainCamera = Camera.main;
    }

    private void Update()
    {
        //Face transform towards camera position
        transform.LookAt(mainCamera.transform.position);
    }
}
