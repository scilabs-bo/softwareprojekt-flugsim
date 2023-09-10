using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    float rollInput;
    float rollSpeed = 90f;
    float rollAcceleration = 3.5f;
    public float Geschwindigkeit = 1000.0f;
    public float Drehgeschwindigkeit = 30.0f;
    public float Kippgeschwindigkeit = 30.0f;
    public float MaxKippung = 45.0f;


    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        float bewegungVertikal = Input.GetAxis("Vertical");
        float drehungHorizontal = Input.GetAxis("Horizontal");

        Vector3 bewegung = transform.forward * bewegungVertikal;
        Vector3 drehung = new Vector3(0.0f, drehungHorizontal, 0.0f) * Drehgeschwindigkeit * Time.deltaTime;
        Vector3 kippung = new Vector3(0.0f, 0.0f, -drehungHorizontal) * Kippgeschwindigkeit * Time.deltaTime;

        transform.position = transform.position + bewegung * Geschwindigkeit * Time.deltaTime;
        transform.Rotate(drehung);

        // Kippung auf der Z-Achse auf maximal 45 Grad begrenzen
        float zRotation = transform.localEulerAngles.z;
        zRotation = (zRotation > 180) ? zRotation - 360 : zRotation; // Umwandlung in Bereich -180 bis +180
        if (Mathf.Abs(zRotation) > MaxKippung)
        {
            transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, MaxKippung * Mathf.Sign(zRotation));
        }
    }

    /*
    float drehungHorizontal = Input.GetAxis("Horizontal");
    Vector3 drehung = new Vector3(0.0f, drehungHorizontal, 0.0f) * Drehgeschwindigkeit * Time.deltaTime;
    transform.Rotate(drehung, Space.Self);

    float bewegungVertikal = Input.GetAxis("Vertical");
    Vector3 bewegung = transform.forward * bewegungVertikal;
    transform.position = transform.position + bewegung * Geschwindigkeit * Time.deltaTime;

    rollInput = Mathf.Lerp(rollInput, Input.GetAxisRaw("Roll"), rollAcceleration * Time.deltaTime);
    transform.Rotate(0, 0, rollInput * rollSpeed * Time.deltaTime, Space.Self);

}*/
}
