// AZMLib/Output/Channels/SerialOutputChannel.cs
using System.IO.Ports;
using UCNLDrivers;
using UCNLNMEA;

namespace AZMLib.Output
{
    public class SerialOutputChannel : IOutputChannel
    {
        private readonly string _portName;
        private readonly BaudRate _baudRate;
        private NMEASerialPort? _port;

        public string Id { get; }
        public bool IsActive => _port?.IsOpen ?? false;

        public SerialOutputChannel(string id, string portName, BaudRate baudRate)
        {
            Id = id;
            _portName = portName;
            _baudRate = baudRate;
        }

        public void Start()
        {
            if (_port != null && _port.IsOpen) return;

            _port = new NMEASerialPort(new SerialPortSettings(
                _portName, _baudRate,
                Parity.None, DataBits.dataBits8, StopBits.One, Handshake.None));
            _port.Open();
        }

        public void Stop()
        {
            try { _port?.Close(); } catch { }
        }

        public void Send(string data)
        {
            if (_port?.IsOpen == true)
                _port.SendData(data);
        }

        public void Dispose()
        {
            Stop();
            _port?.Dispose();
            _port = null;
        }
    }
}