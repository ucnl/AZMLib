// AZMLib/Output/Channels/UDPOutputChannel.cs
using System.Net;
using UCNLDrivers;

namespace AZMLib.Output
{
    public class UDPOutputChannel : IOutputChannel
    {
        private readonly IPEndPoint _endpoint;
        private UDPTranslator? _translator;

        public string Id { get; }
        public bool IsActive => _translator != null;

        public UDPOutputChannel(string id, IPEndPoint endpoint)
        {
            Id = id;
            _endpoint = endpoint;
        }

        public void Start()
        {
            _translator = new UDPTranslator(_endpoint.Port, _endpoint.Address);
        }

        public void Stop()
        {
            _translator = null;
        }

        public void Send(string data)
        {
            _translator?.Send(data);
        }

        public void Dispose()
        {
            _translator = null;
        }
    }
}