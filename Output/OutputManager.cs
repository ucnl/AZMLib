// AZMLib/Output/OutputManager.cs
namespace AZMLib.Output
{
    public class OutputManager : IDisposable
    {
        private readonly List<IOutputChannel> _channels = new();
        private readonly IOutputFormatter? _formatter;

        public UDPPerBeaconChannel? PerBeaconChannel { get; private set; }

        public event Action<string>? OnLineGenerated;

        public OutputManager(IOutputFormatter? formatter = null)
        {
            _formatter = formatter;
        }

        public void AddChannel(IOutputChannel channel)
        {
            _channels.Add(channel);
            channel.Start();
        }

        public void RemoveChannel(string id)
        {
            var channel = _channels.FirstOrDefault(c => c.Id == id);
            if (channel != null)
            {
                try { channel.Stop(); } catch { }
                try { channel.Dispose(); } catch { }
                _channels.Remove(channel);
            }
        }

        public void SetPerBeaconChannel(UDPPerBeaconChannel channel)
        {
            PerBeaconChannel = channel;
        }

        public void OnBeaconData(ResponderBeacon beacon, AZMTranscieverState state, DateTime timestamp)
        {
            if (_formatter != null)
            {
                foreach (var line in _formatter.FormatBeacon(beacon, state, timestamp))
                    Broadcast(line);
            }

            var rawLine = beacon.ToString();
            OnLineGenerated?.Invoke(rawLine);
            PerBeaconChannel?.SendToBeacon(beacon.Address, rawLine);
        }

        public void OnStationData(AZMTranscieverState state, DateTime timestamp)
        {
            if (_formatter != null)
            {
                var line = _formatter.FormatStation(state, timestamp);
                Broadcast(line);
            }

            var rawLine = state.StationParametersToString();
            OnLineGenerated?.Invoke(rawLine);
        }

        private void Broadcast(string data)
        {
            foreach (var ch in _channels)
            {
                try { ch.Send(data); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OutputManager] {ch.Id}: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            foreach (var ch in _channels)
            {
                try { ch.Stop(); } catch { }
                try { ch.Dispose(); } catch { }
            }
            _channels.Clear();

            try { PerBeaconChannel?.Stop(); } catch { }
            try { PerBeaconChannel?.Dispose(); } catch { }
            PerBeaconChannel = null;
        }
    }
}