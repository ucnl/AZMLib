// AZMLib/AuxSettings.cs
using UCNLDrivers;

namespace AZMLib
{
    public class AuxSettings : SimpleSettingsContainer
    {
        #region AZM Port

        public string AzmPrefPortName = string.Empty;
        public BaudRate AzmPortBaudrate = BaudRate.baudRate9600;

        #endregion

        #region AUX1

        public bool Aux1Enabled = false;
        public bool Aux1Alternative = false;
        public string Aux1PrefPortName = string.Empty;
        public BaudRate Aux1PortBaudrate = BaudRate.baudRate9600;

        #endregion

        #region AUX2

        public bool Aux2Enabled = false;
        public string Aux2PrefPortName = string.Empty;
        public BaudRate Aux2PortBaudrate = BaudRate.baudRate9600;

        #endregion        

        #region Chain

        /// <summary>
        /// Порядок активации в цепочке. Если пусто — Simultaneous.
        /// Например: ["azm", "aux1", "aux2"]
        /// </summary>
        public List<string> ActivationOrder = new();

        #endregion

        #region SimpleSettingsContainer

        public override void SetDefaults()
        {
            AzmPrefPortName = string.Empty;
            AzmPortBaudrate = BaudRate.baudRate9600;

            Aux1Enabled = false;
            Aux1Alternative = false;
            Aux1PrefPortName = string.Empty;
            Aux1PortBaudrate = BaudRate.baudRate9600;

            Aux2Enabled = false;
            Aux2PrefPortName = string.Empty;
            Aux2PortBaudrate = BaudRate.baudRate9600;

            ActivationOrder = new();
        }

        #endregion
    }
}