// AZMLib/Output/Formatters/RawProtocolFormatter.cs
namespace AZMLib.Output
{
    public class RawProtocolFormatter : IOutputFormatter
    {
        public string Id => "RawProtocol";

        public IEnumerable<string> FormatBeacon(ResponderBeacon beacon, AZMTranscieverState state, DateTime timestamp)
        {
            yield return beacon.ToString();
        }

        public string FormatStation(AZMTranscieverState state, DateTime timestamp)
        {
            return state.StationParametersToString();
        }
    }
}