// AZMLib/Output/OutputSettings.cs
using System.Net;
using UCNLDrivers;

namespace AZMLib.Output
{
    public class OutputSettings : SimpleSettingsContainer
    {
        #region Format

        public bool IsPSIMSSBEnabled = false;

        #endregion

        #region Serial

        public bool SerialEnabled = false;
        public string SerialPortName = string.Empty;
        public BaudRate SerialBaudrate = BaudRate.baudRate9600;

        #endregion

        #region UDP Broadcast

        public bool UDPEnabled = false;
        public IPEndPoint? UDPBroadcastEndpoint = null;

        #endregion

        #region Individual Beacon UDP

        public Dictionary<REMOTE_ADDR_Enum, IPEndPoint> BeaconEndpoints = new();

        #endregion

        #region SimpleSettingsContainer

        public override void SetDefaults()
        {
            IsPSIMSSBEnabled = false;

            SerialEnabled = false;
            SerialPortName = string.Empty;
            SerialBaudrate = BaudRate.baudRate9600;

            UDPEnabled = false;
            UDPBroadcastEndpoint = new IPEndPoint(new IPAddress(new byte[] { 255, 255, 255, 255 }), 28128);

            BeaconEndpoints = new();
        }

        #endregion
    }
}