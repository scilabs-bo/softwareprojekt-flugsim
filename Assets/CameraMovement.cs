using CesiumForUnity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CameraMovement : MonoBehaviour
{
    float Speed = 100f;
    float RotationSpeed = 30.0f;
    float TiltSpeed = 30.0f;
    float MaxTilt = 15.0f;
    float currentVelocity;
    CesiumCameraController cameraController;


    // Start is called before the first frame update
    void Start()
    {
        cameraController = gameObject.GetComponent<CesiumCameraController>();
        cameraController.defaultMaximumSpeed = 7;
    }

    // Update is called once per frame
    void Update()
    {
        float verticalMovement = 0;
        float horizontalRotation = Input.GetAxis("Horizontal");
        float verticalRotation = 0.0f;


        if (Input.GetKey(KeyCode.F1)) 
        {
            cameraController.defaultMaximumSpeed = 3;
        }
        if (Input.GetKey(KeyCode.F2)) 
        {
            cameraController.defaultMaximumSpeed = 15;
        }

        if (Input.GetKey(KeyCode.F3)) 
        {
            cameraController.defaultMaximumSpeed = 50;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            verticalRotation = 1.0f;
        }
        else if (Input.GetKey(KeyCode.UpArrow))
        {
            verticalRotation = -1.0f;
        }

        Vector3 movement = transform.forward * verticalMovement;
        Vector3 rotation = new Vector3(verticalRotation, horizontalRotation, 0.0f) * RotationSpeed * Time.deltaTime;
        Vector3 tilt = new Vector3(0.0f, 0.0f, -horizontalRotation) * TiltSpeed * Time.deltaTime;

        transform.position = transform.position + movement * Speed * Time.deltaTime;
        transform.Rotate(rotation);
        transform.Rotate(tilt);

        // Limit the tilt on the Z-axis to a maximum of 45 degrees
        float zRotation = transform.localEulerAngles.z;
        zRotation = (zRotation > 180) ? zRotation - 360 : zRotation; // Conversion to range -180 to +180
        // Debug.Log(MaxTilt);
        if (Mathf.Abs(zRotation) > MaxTilt)
        {
            transform.localEulerAngles = new Vector3(0, transform.localEulerAngles.y, MaxTilt * Mathf.Sign(zRotation));
        }

        if (horizontalRotation == 0)
        {
            float zReset = Mathf.SmoothDampAngle(zRotation, 0, ref currentVelocity, 8f / RotationSpeed);
            float zRotationChange = zReset - zRotation;
            //Debug.Log(zRotationChange);
            transform.Rotate(0, 0, zRotationChange);
        }
    }
}
