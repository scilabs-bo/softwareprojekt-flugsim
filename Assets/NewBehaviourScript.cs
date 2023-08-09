using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public float camSpeed = -0.5f;

    private float x;
    private float y;
    private Vector3 rotateValue;
    private Camera _camera;
    // Start is called before the first frame update
    void Start()
    {
        _camera = GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        x = Input.GetAxis("Mouse X");
        y = Input.GetAxis("Mouse Y");
        Debug.Log(x + ":" + y);
        rotateValue = new Vector3(y, x, 0);
        transform.eulerAngles = transform.eulerAngles - rotateValue;
        transform.eulerAngles += rotateValue * camSpeed;
    }
}
