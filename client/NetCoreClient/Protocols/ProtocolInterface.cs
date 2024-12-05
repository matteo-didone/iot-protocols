using System;

namespace NetCoreClient.Protocols
{
    public interface IProtocolInterface : IDisposable
    {
        event EventHandler<CommandEventArgs>? OnCommandReceived;
        void Send(string data);
        void SendCommand(string topic, string payload);
        void Subscribe(string topic);
        void Unsubscribe(string topic);
    }

    public class CommandEventArgs : EventArgs
    {
        public string Topic { get; }
        public string Payload { get; }
        public DateTime Timestamp { get; }

        public CommandEventArgs(string topic, string payload)
        {
            Topic = topic;
            Payload = payload;
            Timestamp = DateTime.UtcNow;
        }
    }
}