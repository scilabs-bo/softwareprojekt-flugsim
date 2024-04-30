using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Assets;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    private float RotationSpeed = 30.0f;
    private float MaxTilt = 15.0f;
    private float currentVelocity;
    private float _maxSpeed = 50.0f;
    private float _acceleration = 20000.0f;
    private float _deceleration = 9999999959.0f;
    private float verticalMovement = 0;
    private float horizontalRotation = 0;
    private float verticalRotation = 0;
    private Vector3 _velocity = Vector3.zero;

    private JoystickControls joystickControls;
    private CesiumCameraController cameraController;
    private CesiumGeoreference _georeference;
    private CesiumGlobeAnchor _globeAnchor;
    private CharacterController _controller;
    private Config config;
    private SimulatorController simulatorController;

    // read Input device values
    void JoystickMovement(InputAction.CallbackContext context)
    {
        horizontalRotation = context.ReadValue<Vector2>().x;
        verticalRotation = -context.ReadValue<Vector2>().y;
    }

    void ThrusterMovement(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();
        // converting values to avoid negative values 
        verticalMovement = (-value + 1) / 2;
    }

    void InititalizeInputActions()
    {
        // setup the input actions 
        joystickControls = new JoystickControls();
        joystickControls.Gameplay.Enable();
        joystickControls.Gameplay.Thruster.performed += ThrusterMovement;
        joystickControls.Gameplay.Joystick.performed += JoystickMovement;
        joystickControls.Gameplay.Left.performed += context => horizontalRotation = -context.ReadValue<float>();
        joystickControls.Gameplay.Right.performed += context => horizontalRotation = context.ReadValue<float>();
        joystickControls.Gameplay.Up.performed += context => verticalRotation = context.ReadValue<float>();
        joystickControls.Gameplay.Down.performed += context => verticalRotation = -context.ReadValue<float>();
        joystickControls.Gameplay.ResetScene.performed += ResetScene;
        joystickControls.Gameplay.MaxSpeed1.performed += context => this._maxSpeed = 3;
        joystickControls.Gameplay.MaxSpeed2.performed += context => this._maxSpeed = 15;
        joystickControls.Gameplay.MaxSpeed3.performed += context => this._maxSpeed = 50;

        joystickControls.Gameplay.Thruster.canceled += ThrusterMovement;
        joystickControls.Gameplay.Joystick.canceled += JoystickMovement;        
        joystickControls.Gameplay.Left.canceled += context => horizontalRotation = 0;        
        joystickControls.Gameplay.Right.canceled += context => horizontalRotation = 0;
        joystickControls.Gameplay.Up.canceled += context => verticalRotation = 0;       
        joystickControls.Gameplay.Down.canceled += context => verticalRotation = 0;

    }

    // Start is called before the first frame update
    void Start()
    {
        // load config
        string json = File.ReadAllText(Application.dataPath + "/StreamingAssets/config.json");
        config = JsonUtility.FromJson<Config>(json);

        InitializeCameraController();
        InitializeSimulator();
        InititalizeInputActions();
    }

    void InitializeSimulator()
    {
        Debug.Log("Flightsimulator started");
        // starting the simulator
        simulatorController = new SimulatorController(config); 
        simulatorController.sendNativeCommand((byte)SimulatorController.nativeCommand.ToState, (byte)SimulatorController.state.ToMotion);
        // set-Volume - movement intensity 
        simulatorController.sendNativeCommand((byte)SimulatorController.nativeCommand.SetVolume, (byte)config.SimulatorVolume);

    }

    void InitializeCameraController()
    {
        // this Code is borrowed from Cesium
        cameraController = gameObject.GetComponent<CesiumCameraController>();
        cameraController.defaultMaximumSpeed = 7;
        this._georeference = this.gameObject.GetComponentInParent<CesiumGeoreference>();
        this._globeAnchor = this.gameObject.GetComponentInParent<CesiumGlobeAnchor>();
         
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

    // this method will be called when this programm is closed 
    void OnApplicationQuit()
    {
        Debug.Log("Shutting down simulator..");
        simulatorController.sendNativeCommand((byte)SimulatorController.nativeCommand.ToState, (byte)SimulatorController.state.ToReady);
        System.Threading.Thread.Sleep(5000);
        Debug.Log("Shutting down simulator..");
    }

    // when you are stuck then you can restart the scene
    void ResetScene(InputAction.CallbackContext context)
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log("Horizontal: " + horizontalRotation + " Vertical: " + verticalMovement + " Rotation: " + verticalRotation);
        Vector3 movementInput = new Vector3(0.0f, 0.0f, verticalMovement);
        Move(movementInput);

        // calculate vector for tilt
        Vector3 tilt = new Vector3(0.0f, 0.0f, -horizontalRotation) * RotationSpeed * Time.deltaTime;
        this.Rotate(horizontalRotation, verticalRotation);
        float zRotation = transform.localEulerAngles.z;
        zRotation = (zRotation > 180) ? zRotation - 360 : zRotation; // Conversion to range -180 to +180

        // Limit the tilt on the Z-axis to a maximum of 15 degrees
        if (Mathf.Abs(zRotation) < MaxTilt)
        {
            if (_maxSpeed > 15)
                transform.Rotate(tilt);
        }

        // if no input go back to normal
        if (horizontalRotation == 0)
        {
            // smooth transition
            float zReset = Mathf.SmoothDampAngle(zRotation, 0, ref currentVelocity, 8f / RotationSpeed);
            float zRotationChange = zReset - zRotation;
            transform.Rotate(0, 0, zRotationChange);
        }

        // send position to the simulator
        simulatorController.sendNativeTelemetry((byte)SimulatorController.telemetryCommand.Acceleration_Orientation,0.05f,0.05f,0,horizontalRotation,-verticalRotation,0);
    }

    private void Move(Vector3 movementInput)
    {
        Vector3 inputDirection =
            this.transform.right * movementInput.x + this.transform.forward * movementInput.z;

        // update cesium georeference after movement 
        if (this._georeference != null)
        {
            double3 positionECEF = this._globeAnchor.positionGlobeFixed;
            double3 upECEF = CesiumWgs84Ellipsoid.GeodeticSurfaceNormal(positionECEF);
            double3 upUnity =
                this._georeference.TransformEarthCenteredEarthFixedDirectionToUnity(upECEF);
            
            // moving along the y-axis 
            inputDirection = (float3)inputDirection + (float3)upUnity * movementInput.y;
        }

        if (inputDirection != Vector3.zero)
        {
            if (this._velocity.magnitude > 0.0f)
            { 
                Vector3 directionChange = inputDirection - this._velocity.normalized;
                this._velocity +=
                    directionChange * this._velocity.magnitude * Time.deltaTime;
            }
            this._velocity += inputDirection * this._acceleration * Time.deltaTime;

            // stay below max speed 
            this._velocity = Vector3.ClampMagnitude(this._velocity, this._maxSpeed * Math.Abs(verticalMovement));
        }
        else
        {
            // decelerate  
            float speed = Mathf.Max(this._velocity.magnitude - this._deceleration * Time.deltaTime,0.0f);
            this._velocity = Vector3.ClampMagnitude(this._velocity, speed);
        }

        if (this._velocity != Vector3.zero)
        {
            // move CharacterController
            this._controller.Move(this._velocity * Time.deltaTime);
 
            if (!this._globeAnchor.detectTransformChanges)
            {
                this._globeAnchor.Sync();
            }
        }
    }

    private void Rotate(float horizontalRotation, float verticalRotation)
    {
        if (horizontalRotation == 0.0f && verticalRotation == 0.0f)
        {
            return;
        }

        float valueX = verticalRotation * this.RotationSpeed * Time.smoothDeltaTime;
        float valueY = horizontalRotation * this.RotationSpeed * Time.smoothDeltaTime;

        // calculate camera rotation
        float rotationX = this.transform.localEulerAngles.x;
        if (rotationX <= 90.0f)
        {
            rotationX += 360.0f;
        }

        float newRotationX = Mathf.Clamp(rotationX - valueX, 270.0f, 450.0f);
        float newRotationY = this.transform.localEulerAngles.y + valueY;

        cameraController.transform.localRotation =
            Quaternion.Euler(newRotationX, newRotationY, this.transform.localEulerAngles.z);
    }

    
}

