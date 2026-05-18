// AZMLib/Output/IOutputChannel.cs
namespace AZMLib.Output
{
    public interface IOutputChannel : IDisposable
    {
        string Id { get; }
        bool IsActive { get; }
        void Send(string data);
        void Start();
        void Stop();
    }
}