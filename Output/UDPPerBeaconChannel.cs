// AZMLib/Output/Channels/UDPPerBeaconChannel.cs
using System.Net;
using UCNLDrivers;

namespace AZMLib.Output
{
    public class UDPPerBeaconChannel : IOutputChannel
    {
        private readonly Dictionary<REMOTE_ADDR_Enum, UDPTranslator> _translators = new();

        public string Id { get; }
        public bool IsActive => _translators.Count > 0;

        public UDPPerBeaconChannel(string id)
        {
            Id = id;
        }

        public void SetBeaconEndpoint(REMOTE_ADDR_Enum addr, IPEndPoint ep)
        {
            _translators[addr] = new UDPTranslator(ep.Port, ep.Address);
        }

        public void RemoveBeacon(REMOTE_ADDR_Enum addr)
        {
            _translators.Remove(addr);
        }

        public void Start() { }

        public void Stop()
        {
            _translators.Clear();
        }

        public void SendToBeacon(REMOTE_ADDR_Enum addr, string data)
        {
            if (_translators.TryGetValue(addr, out var translator))
                translator.Send(data);
        }

        public void Send(string data)
        {
            // Не используется — каждый маяк шлёт индивидуально
        }

        public void Dispose()
        {
            Stop();
        }
    }
}