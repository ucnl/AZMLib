// AZMLib/Output/IOutputFormatter.cs
namespace AZMLib.Output
{
    public interface IOutputFormatter
    {
        string Id { get; }
        IEnumerable<string> FormatBeacon(ResponderBeacon beacon, AZMTranscieverState state, DateTime timestamp);
        string FormatStation(AZMTranscieverState state, DateTime timestamp);
    }
}