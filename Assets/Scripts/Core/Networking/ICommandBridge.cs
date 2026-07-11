namespace KitchenDesigner.Core
{
    public interface ICommandBridge
    {
        void Start();
        void Stop();
        bool IsRunning { get; }
        void SendResponse(string json);
        event System.Action<string> OnCommandReceived;
    }
}
