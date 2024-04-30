using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Assets
{
    class SimulatorController
    {
        public enum telemetryCommand { Power = 0, Angle = 1, Length = 2, Platform = 3, Acceleration = 4, Acceleration_Orientation = 5, Speed_Orientation = 6 };
        public enum nativeCommand { ToState = 1, SetVolume = 2, SetPreset = 3, SetFilter = 4, ReadState = 5, ReadVolume = 6, ReadPreset = 7, ReadFilter = 8 };
        public enum state { ToMotion = 0, ToReady = 1 };

        Config c;
        public SimulatorController(Config config)
        {
            c = config; 
        }
        int sendPacketNative(byte[] data)
        {
            UdpClient udpClient = new UdpClient();
            IPAddress ipAddress = IPAddress.Parse(c.SimulatorIP);
            int port = c.SimulatorPort;
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, port);
            return udpClient.Send(data, data.Length, ipEndPoint);
        }

        public void sendNativeCommand(byte cmd, byte para)
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

        public void sendNativeTelemetry(byte type, float tx, float ty, float tz, float rx, float ry, float rz)
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
}
