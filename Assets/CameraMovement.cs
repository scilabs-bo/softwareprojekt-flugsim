using CesiumForUnity;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


public class CameraMovement : MonoBehaviour
{
    float Speed = 100f;
    float RotationSpeed = 30.0f;
    float TiltSpeed = 30.0f;
    float MaxTilt = 15.0f;
    float currentVelocity;
    CesiumCameraController cameraController;
    private CesiumGeoreference _georeference;
    private CesiumGlobeAnchor _globeAnchor;
    private Camera _camera;
    private float _initialNearClipPlane;
    private float _initialFarClipPlane;

    private CharacterController _controller;

    private Vector3 _velocity = Vector3.zero;
    private float _lookSpeed = 10.0f;

    // These numbers are borrowed from Cesium for Unreal.
    private float _acceleration = 20000.0f;
    private float _deceleration = 9999999959.0f;
    private float _maxRaycastDistance = 1000 * 1000; // 1000 km;

    private float _maxSpeed = 100.0f; // Maximum speed with the speed multiplier applied.
    private float _maxSpeedPreMultiplier = 0.0f; // Max speed without the multiplier applied.
    private AnimationCurve _maxSpeedCurve;

    private float _speedMultiplier = 1.0f;
    private float _speedMultiplierIncrement = 1.5f;

    // If the near clip gets too large, Unity will throw errors. Keeping it 
    // at this value works fine even when the far clip plane gets large.
    private float _maximumNearClipPlane = 1000.0f;
    private float _maximumFarClipPlane = 500000000.0f;

    // The maximum ratio that the far clip plane is allowed to be larger
    // than the near clip plane. The near clip plane is set so that this
    // ratio is never exceeded.
    private float _maximumNearToFarRatio = 100000.0f;


    // Start is called before the first frame update
    void Start()
    {
        cameraController = gameObject.GetComponent<CesiumCameraController>();
        cameraController.defaultMaximumSpeed = 7;
        this._georeference = this.gameObject.GetComponentInParent<CesiumGeoreference>();
        this._globeAnchor = this.gameObject.GetComponentInParent<CesiumGlobeAnchor>();
        InitializeController();
    }


    void InitializeController()
    {
        if (this.gameObject.GetComponent<CharacterController>() != null)
        {
            Debug.LogWarning(
                "A CharacterController component was manually " +
                "added to the CesiumCameraController's game object. " +
                "This may interfere with the CesiumCameraController's movement.");

            this._controller = this.gameObject.GetComponent<CharacterController>();
        }
        else
        {
            this._controller = this.gameObject.AddComponent<CharacterController>();
            this._controller.hideFlags = HideFlags.HideInInspector;
        }

        this._controller.radius = 1.0f;
        this._controller.height = 1.0f;
        this._controller.center = Vector3.zero;
        this._controller.detectCollisions = true;
    }

    // Update is called once per frame
    void Update()
    {
        float verticalMovement = Input.GetAxis("Vertical");
        float horizontalRotation = Input.GetAxis("Horizontal");
        //float verticl = Input.GetAxis("Vertical");
        Debug.Log("Horizontal: " + horizontalRotation + " Vertical: " + verticalMovement);

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
        Move(movement);
        Vector3 rotation = new Vector3(verticalRotation, horizontalRotation, 0.0f) * RotationSpeed * Time.deltaTime;
        Vector3 tilt = new Vector3(0.0f, 0.0f, -horizontalRotation) * TiltSpeed * Time.deltaTime;

        //transform.position = transform.position + movement * Speed * Time.deltaTime;
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

    /// <summary>
    /// Moves the controller with the given player input.
    /// </summary>
    /// <remarks>
    /// The x-coordinate affects movement along the transform's right axis.
    /// The y-coordinate affects movement along the georeferenced up axis.
    /// The z-coordinate affects movement along the transform's forward axis.
    /// </remarks>
    /// <param name="movementInput">The player input.</param>
    private void Move(Vector3 movementInput)
    {
        Vector3 inputDirection =
            this.transform.right * movementInput.x + this.transform.forward * movementInput.z;

        if (this._georeference != null)
        {
            double3 positionECEF = this._globeAnchor.positionGlobeFixed;
            double3 upECEF = CesiumWgs84Ellipsoid.GeodeticSurfaceNormal(positionECEF);
            double3 upUnity =
                this._georeference.TransformEarthCenteredEarthFixedDirectionToUnity(upECEF);

            inputDirection = (float3)inputDirection + (float3)upUnity * movementInput.y;
        }

        if (inputDirection != Vector3.zero)
        {
            // If the controller was already moving, handle the direction change
            // separately from the magnitude of the velocity.
            if (this._velocity.magnitude > 0.0f)
            {
                Vector3 directionChange = inputDirection - this._velocity.normalized;
                this._velocity +=
                    directionChange * this._velocity.magnitude * Time.deltaTime;
            }

            this._velocity += inputDirection * this._acceleration * Time.deltaTime;
            this._velocity = Vector3.ClampMagnitude(this._velocity, this._maxSpeed);
        }
        else
        {
            // Decelerate
            float speed = Mathf.Max(
                this._velocity.magnitude - this._deceleration * Time.deltaTime,
                0.0f);

            this._velocity = Vector3.ClampMagnitude(this._velocity, speed);
        }

        if (this._velocity != Vector3.zero)
        {
            this._controller.Move(this._velocity * Time.deltaTime);

            // Other controllers may disable detectTransformChanges to control their own
            // movement, but the globe anchor should be synced even if detectTransformChanges
            // is false.
            if (!this._globeAnchor.detectTransformChanges)
            {
                this._globeAnchor.Sync();
            }
        }
    }
}
