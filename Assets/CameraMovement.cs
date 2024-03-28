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
    float RotationSpeed = 30.0f;
    float TiltSpeed = 30.0f;
    float MaxTilt = 15.0f;
    float currentVelocity;
    JoystickControls joystickControls;
    CesiumCameraController cameraController;
    private CesiumGeoreference _georeference;
    private CesiumGlobeAnchor _globeAnchor;

    private CharacterController _controller;

    private Vector3 _velocity = Vector3.zero;
    private float _lookSpeed = 30.0f;

    // These numbers are borrowed from Cesium for Unreal.
    private float _acceleration = 20000.0f;
    private float _deceleration = 9999999959.0f;

    private float _maxSpeed = 50.0f; // Maximum speed with the speed multiplier applied.
    enum telemetryCommand { Power = 0, Angle = 1, Length = 2, Platform = 3, Acceleration = 4, Acceleration_Orientation = 5, Speed_Orientation = 6 };
    enum nativeCommand { ToState = 1, SetVolume = 2, SetPreset = 3, SetFilter = 4, ReadState = 5, ReadVolume = 6, ReadPreset = 7, ReadFilter = 8 };
    enum state { ToMotion = 0, ToReady = 1 };
    private Config config;

    private float verticalMovement = 0;
    private float horizontalRotation = 0;
    private float verticalRotation = 0;

    void JoystickMovement(InputAction.CallbackContext context)
    {
        horizontalRotation = context.ReadValue<Vector2>().x;
        verticalRotation = -context.ReadValue<Vector2>().y;
    }

    void ThrusterMovement(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();
        verticalMovement = (value + 1) / 2;
    }

    void InititalizeInputActions()
    {
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
        //load config
        string json = File.ReadAllText(Application.dataPath + "/StreamingAssets/config.json");
        config = JsonUtility.FromJson<Config>(json);
        /*
        config = new Config();
        config.SimulatorIP = "192.168.0.147";
        config.SimulatorPort = 50001;
        config.SimulatorVolume = 10;
        string json = JsonUtility.ToJson(config);
        File.WriteAllText(Application.dataPath + "/StreamingAssets/config.json", json);
        */

        InitializeController();
        InitializeSimulator();
        InititalizeInputActions();
    }

    void InitializeSimulator()
    {
        Debug.Log("Flightsimulator started");
        // starten
        sendNativeCommand((byte)nativeCommand.ToState, (byte)state.ToMotion);
        // set-Volume - bewegen
        sendNativeCommand((byte)nativeCommand.SetVolume, (byte)config.SimulatorVolume);

    }

    void InitializeController()
    {
        // unklar
        cameraController = gameObject.GetComponent<CesiumCameraController>();
        cameraController.defaultMaximumSpeed = 7;
        this._georeference = this.gameObject.GetComponentInParent<CesiumGeoreference>();
        this._globeAnchor = this.gameObject.GetComponentInParent<CesiumGlobeAnchor>();
        // wenn CharacterController Komponente != null, dann als lokale Referenz speichern 
        if (this.gameObject.GetComponent<CharacterController>() != null)
        {
            Debug.LogWarning(
                "A CharacterController component was manually " +
                "added to the CesiumCameraController's game object. " +
                "This may interfere with the CesiumCameraController's movement.");

            // CharacterController wird in die Variable ._controller gespeichert
            this._controller = this.gameObject.GetComponent<CharacterController>();
        }
        else
        {
            this._controller = this.gameObject.AddComponent<CharacterController>();

            // ! nochmal nachgucken was die folgenden Zeile genau macht !
            this._controller.hideFlags = HideFlags.HideInInspector;
        }

        this._controller.radius = 1.0f;
        this._controller.height = 1.0f;
        this._controller.center = Vector3.zero;
        this._controller.detectCollisions = true;
    }

    void OnApplicationQuit()
    {
        Debug.Log("Shutting down simulator..");
        sendNativeCommand((byte)nativeCommand.ToState, (byte)state.ToReady);
        System.Threading.Thread.Sleep(5000);
        Debug.Log("Shutting down simulator..");
    }

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
        // neues Vektorobjekt wird berechnet anhand der Kippgeschwindigkeit und der Eingabewerte (kippen beim drehen)
        Vector3 tilt = new Vector3(0.0f, 0.0f, -horizontalRotation) * TiltSpeed * Time.deltaTime;


        this.Rotate(horizontalRotation, verticalRotation);

        // Limit the tilt on the Z-axis to a maximum of 45 degrees
        // die Rotation in grad Zahlen wird übergeben (Z-Achse die von vorne nach hinten f�hrt) 
        float zRotation = transform.localEulerAngles.z;
        // die Gradzahlen sollen zwischen -180 und +180 liegen, dementsprechnend wird es hier umgerechnet 
        zRotation = (zRotation > 180) ? zRotation - 360 : zRotation; // Conversion to range -180 to +180
        // Debug.Log(MaxTilt);

        // falls die Rotation größer als 15 Grad ist, dann wird stattdessen weiterhin nur 15 Grad verwendet
        if (Mathf.Abs(zRotation) < MaxTilt)
        {
            if (_maxSpeed > 15)
                transform.Rotate(tilt);
        }

        // wenn keine Eingabe zur Kippung stattfindet, dann tue die folgenden Dinge...
        if (horizontalRotation == 0)
        {
            // es soll wieder langsam in die Anfangsposition kommen, hierbei wird der Übergangswert berechnet
            float zReset = Mathf.SmoothDampAngle(zRotation, 0, ref currentVelocity, 8f / RotationSpeed);
            float zRotationChange = zReset - zRotation;
            //Debug.Log(zRotationChange);
            transform.Rotate(0, 0, zRotationChange);
        }

        // Simulator Position senden
        sendNativeTelemetry((byte)telemetryCommand.Acceleration_Orientation,0.1f,0,0,horizontalRotation,0,0);
    }

    private void Move(Vector3 movementInput)
    {
        // rechts-links und vorwärts-rückwärts Eingabewerte werden gespeichert
        Vector3 inputDirection =
            this.transform.right * movementInput.x + this.transform.forward * movementInput.z;

        if (this._georeference != null)
        {
            // unklar!
            double3 positionECEF = this._globeAnchor.positionGlobeFixed;
            double3 upECEF = CesiumWgs84Ellipsoid.GeodeticSurfaceNormal(positionECEF);
            double3 upUnity =
                this._georeference.TransformEarthCenteredEarthFixedDirectionToUnity(upECEF);
            // Bewegung anhand der Y-Achse
            inputDirection = (float3)inputDirection + (float3)upUnity * movementInput.y;
        }

        if (inputDirection != Vector3.zero)
        {
            // If the controller was already moving, handle the direction change
            // separately from the magnitude of the velocity.
            if (this._velocity.magnitude > 0.0f)
            {
                // 
                Vector3 directionChange = inputDirection - this._velocity.normalized;
                // neue Geschwindigkeit wird in Abhängigkeit der alten Geschwindigkeit berechnet 
                this._velocity +=
                    directionChange * this._velocity.magnitude * Time.deltaTime;
            }
            // Geschwindigkeit in Abhängigkeit der Beschleunigung wird berechnet
            this._velocity += inputDirection * this._acceleration * Time.deltaTime;

            // Geschwindigkeit darf Max.Geschwindigkeit nicht überschreiten
            this._velocity = Vector3.ClampMagnitude(this._velocity, this._maxSpeed * Math.Abs(verticalMovement));
        }
        else
        {
            // Decelerate - Geschwindigkeit solange reduzieren bis Sie bei 0 ist 
            float speed = Mathf.Max(
                this._velocity.magnitude - this._deceleration * Time.deltaTime,
                0.0f);
            // aktuller Wert wird übergeben
            this._velocity = Vector3.ClampMagnitude(this._velocity, speed);
        }

        if (this._velocity != Vector3.zero)
        {
            // Bewegungsvektor wird am Controller übergeben damit Bewegung ausgeführt wird
            this._controller.Move(this._velocity * Time.deltaTime);
            // Other controllers may disable detectTransformChanges to control their own
            // movement, but the globe anchor should be synced even if detectTransformChanges
            // is false.
            // unklar  
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

        float valueX = verticalRotation * this._lookSpeed * Time.smoothDeltaTime;
        float valueY = horizontalRotation * this._lookSpeed * Time.smoothDeltaTime;

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

    int sendPacketNative(byte[] data)
    {
        UdpClient udpClient = new UdpClient();
        IPAddress ipAddress = IPAddress.Parse(config.SimulatorIP);
        int port = config.SimulatorPort;
        IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, port);
        return udpClient.Send(data, data.Length, ipEndPoint);
    }

    void sendNativeCommand(byte cmd, byte para)
    {
        byte[] data = new byte[5];
        byte header = 71;
        byte packet_version = 0;
        byte command_type = 67;
        byte command = cmd;
        byte parameter = para;
        data[0] = header;
        data[1] = packet_version;
        data[2] = command_type;
        data[3] = command;
        data[4] = parameter;

        sendPacketNative(data);
    }

    void sendNativeTelemetry(byte type, float tx, float ty, float tz, float rx, float ry, float rz)
    {
        byte header = 71;
        byte packet_version = 0;
        byte motion_type = 77;
        uint timestamp = (uint)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds * 1000;

        byte[] data = new byte[32];
        data[0] = header;
        data[1] = packet_version;
        data[2] = motion_type;
        data[3] = type;
        Array.Copy(BitConverter.GetBytes(timestamp), 0, data, 4, 4);
        Array.Copy(BitConverter.GetBytes(tx), 0, data, 8, 4);
        Array.Copy(BitConverter.GetBytes(ty), 0, data, 12, 4);
        Array.Copy(BitConverter.GetBytes(tz), 0, data, 16, 4);
        Array.Copy(BitConverter.GetBytes(rx), 0, data, 20, 4);
        Array.Copy(BitConverter.GetBytes(ry), 0, data, 24, 4);
        Array.Copy(BitConverter.GetBytes(rz), 0, data, 28, 4);

        sendPacketNative(data);


    }
}

