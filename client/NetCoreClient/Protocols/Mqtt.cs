using MQTTnet;
using MQTTnet.Client;
using System.Text.Json;
using System.Text;

namespace NetCoreClient.Protocols
{
    class Mqtt : ProtocolInterface, IDisposable
    {
        private readonly IMqttClient mqttClient;
        
        public Mqtt(string brokerEndpoint)
        {
            var mqttFactory = new MqttFactory();
    mqttClient = mqttFactory.CreateMqttClient();
    
    var options = new MqttClientOptionsBuilder()
        .WithTcpServer("localhost", 1883)  // broker locale
        .WithClientId($"water_cooler_{Guid.NewGuid()}")
        .Build();

    mqttClient.ConnectAsync(options).GetAwaiter().GetResult();
    Console.WriteLine("Connected to MQTT broker");

            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                string receivedTopic = e.ApplicationMessage.Topic;
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                Console.WriteLine($"Received message on topic {receivedTopic}: {payload}");
                return Task.CompletedTask;
            };
        }

        public async void Send(string data)
        {
            try
            {
                // Estrai il coolerId dal JSON
                var reading = JsonSerializer.Deserialize<JsonElement>(data);
                string coolerId = reading.GetProperty("coolerId").GetString();
                
                // Costruisci il topic corretto
                string topic = $"water_coolers/{coolerId}/readings";

                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(data)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                    .Build();

                await mqttClient.PublishAsync(applicationMessage);
                Console.WriteLine($"Published message to {topic}: {data}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error publishing message: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            if (mqttClient.IsConnected)
            {
                mqttClient.DisconnectAsync().GetAwaiter().GetResult();
                Console.WriteLine("Disconnected from MQTT broker");
            }
        }
    }
}