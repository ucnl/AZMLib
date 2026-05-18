using System.Net;
using UCNLDrivers;

namespace AZMLib
{

    [Obsolete]
    public class AZMSettingsContainer : SimpleSettingsContainer
    {
        #region Properties

        public string azmPrefPortName = string.Empty;
        public BaudRate azmPortBaudrate = BaudRate.baudRate9600;

        public bool aux1Enabled = false;
        public bool aux1Alternative = false;
        public string aux1PrefPortName = string.Empty;
        public BaudRate aux1PortBaudrate = BaudRate.baudRate9600;

        public bool aux2Enabled = false;
        public string aux2PrefPortName = string.Empty;
        public BaudRate aux2PortBaudrate = BaudRate.baudRate9600;

        public bool output_udp_enabled = false;
        public IPEndPoint output_endpoint = new IPEndPoint(new IPAddress(new byte[] { 255, 255, 255, 255 }), 28128);

        public bool outputSerialEnabled = false;
        public string outputSerialPortName = string.Empty;
        public BaudRate outputSerialPortBaudrate = BaudRate.baudRate9600;

        public bool IsPSIMSSBOutputEnabled = false;

        public UInt16 address_mask = 1;
        public int max_dist_m = 1000;

        public double sty_PSU = 0.0;

        public double speedOfSound = double.NaN;

        public double antenna_x_offset_m = 0.0;
        public double antenna_y_offset_m = 0.0;
        public double antenna_angular_offset_deg = 0.0;

        public double USBLMode_DHFilter_MaxSpeed_mps = 1.0;
        public double USBLMode_DHFilter_Threshold_m = 5.0;
        public int USBLMode_DHFilter_FIFO_Size = 8;
        public double USBLMode_SFilter_Threshold = 20.0;
        public int USBLMode_SFilter_FIFO_Size = 4;

        public LBLResponderCoordinatesModeEnum LBLResponderCoordinatesMode = LBLResponderCoordinatesModeEnum.None;

        public (double X, double Y) LBLModeR1Coordinates = (double.NaN, double.NaN);
        public (double X, double Y) LBLModeR2Coordinates = (double.NaN, double.NaN);
        public (double X, double Y) LBLModeR3Coordinates = (double.NaN, double.NaN);

        public double LBLMode_RErr_Threshold_m = 25.0;
        public bool LBLMode_Use_DHFilter = false;
        public double LBLMode_DHFilter_MaxSpeed_mps = 1.0;
        public double LBLMode_DHFilter_Threshold_m = 5.0;
        public int LBLMode_DHFilter_FIFO_Size = 8;
        public bool LBLMode_Use_SFilter = false;
        public double LBLMode_SFilter_Threshold_m = 30.0;
        public int LBLMode_SFilter_FIFO_Size = 4;

        public Dictionary<REMOTE_ADDR_Enum, IPEndPoint> InvidvidualEndpoints = new();

        #endregion

        #region Constructor

        public override void SetDefaults()
        {
            azmPrefPortName = string.Empty;
            azmPortBaudrate = BaudRate.baudRate9600;

            aux1Enabled = false;
            aux1PrefPortName = string.Empty;
            aux1PortBaudrate = BaudRate.baudRate9600;

            aux2Enabled = false;
            aux2PrefPortName = string.Empty;
            aux2PortBaudrate = BaudRate.baudRate9600;

            output_udp_enabled = false;
            output_endpoint = new IPEndPoint(new IPAddress(new byte[] { 255, 255, 255, 255 }), 28128);

            outputSerialEnabled = false;
            outputSerialPortName = string.Empty;
            outputSerialPortBaudrate = BaudRate.baudRate9600;

            IsPSIMSSBOutputEnabled = false;
            
            address_mask = 1;

            max_dist_m = 1000;

            sty_PSU = 0.0;
            speedOfSound = double.NaN; // Auto

            antenna_x_offset_m = 0.0;
            antenna_y_offset_m = 0.0;
            antenna_angular_offset_deg = 0.0;

            USBLMode_DHFilter_MaxSpeed_mps = 1.0;
            USBLMode_DHFilter_Threshold_m = 5.0;
            USBLMode_DHFilter_FIFO_Size = 8;
            USBLMode_SFilter_Threshold = 20.0;
            USBLMode_SFilter_FIFO_Size = 4;

            LBLResponderCoordinatesMode = LBLResponderCoordinatesModeEnum.None;

            LBLModeR1Coordinates = (double.NaN, double.NaN);
            LBLModeR2Coordinates = (double.NaN, double.NaN);
            LBLModeR3Coordinates = (double.NaN, double.NaN);

            LBLMode_RErr_Threshold_m = 25.0;
            LBLMode_Use_DHFilter = false;
            LBLMode_DHFilter_MaxSpeed_mps = 1.0;
            LBLMode_DHFilter_Threshold_m = 5.0;
            LBLMode_DHFilter_FIFO_Size = 8;
            LBLMode_Use_SFilter = false;
            LBLMode_SFilter_Threshold_m = 20.0;
            LBLMode_SFilter_FIFO_Size = 4;
        }

        #endregion
    }
}
