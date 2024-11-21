using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetCoreClient.Protocols
{
    interface ProtocolInterface
    {
        // Metodo esistente per retrocompatibilità
        void Send(string data);

        // Nuovi metodi per supporto MQTT
        void SendCommand(string topic, string payload);
        void Subscribe(string topic);
        void Unsubscribe(string topic);
        
        // Eventi per gestire i comandi in arrivo        event EventHandler<CommandEventArgs> OnCommandReceived;
    }

    // Classe per gli argomenti dell'evento comando
    public class CommandEventArgs : EventArgs
    {
        public string Topic { get; set; }
        public string Payload { get; set; }
        public DateTime Timestamp { get; set; }

        public CommandEventArgs(string topic, string payload)
        {
            Topic = topic;
            Payload = payload;
            Timestamp = DateTime.Now;
        }
    }
}