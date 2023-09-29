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


    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        float verticalMovement = Input.GetAxis("Vertical");
        float horizontalRotation = Input.GetAxis("Horizontal");

        Vector3 movement = transform.forward * verticalMovement;
        Vector3 rotation = new Vector3(0.0f, horizontalRotation, 0.0f) * RotationSpeed * Time.deltaTime;
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
            Debug.Log(zRotationChange);
            transform.Rotate(0, 0, zRotationChange);
        }
    }
}
