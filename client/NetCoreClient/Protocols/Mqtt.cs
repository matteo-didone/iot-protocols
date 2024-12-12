using MQTTnet;
using MQTTnet.Client;
using NetCoreClient.Commands;
using System.Text;
using System.Text.Json;

namespace NetCoreClient.Protocols
{
    public class Mqtt : IDisposable
    {
        private readonly IMqttClient mqttClient;
        public event Action? RequestStatusUpdate;  // Modificato per rimuovere il warning

        public IMqttClient GetMqttClient()
        {
            return mqttClient;
        }

        public Mqtt(string brokerEndpoint)
        {
            var mqttFactory = new MqttFactory();
            mqttClient = mqttFactory.CreateMqttClient();

            mqttClient.ConnectedAsync += e =>
            {
                Console.WriteLine("[LOG] Connected to MQTT broker successfully");
                return Task.CompletedTask;
            };

            mqttClient.DisconnectedAsync += e =>
            {
                Console.WriteLine($"[LOG] Disconnected from MQTT broker: {e.Reason}");
                return Task.CompletedTask;
            };

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerEndpoint, 1883)
                .WithClientId($"water_cooler_{Guid.NewGuid()}")
                .Build();

            try
            {
                mqttClient.ConnectAsync(options).GetAwaiter().GetResult();
                Console.WriteLine("[LOG] Connected to MQTT broker");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to connect to MQTT broker: {ex.Message}");
                throw;
            }

            SetupMessageHandlers();
        }

        private void SetupMessageHandlers()
        {
            mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                string receivedTopic = e.ApplicationMessage.Topic;
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                Console.WriteLine("[DEBUG] Message received from broker:");
                Console.WriteLine($"[DEBUG] Topic: {receivedTopic}");
                Console.WriteLine($"[DEBUG] Payload: {payload}");

                if (receivedTopic == "commands/status")
                {
                    Console.WriteLine("[DEBUG] Status request received");
                    RequestStatusUpdate?.Invoke();
                }
                else if (receivedTopic.StartsWith("commands/"))
                {
                    Console.WriteLine("[DEBUG] Processing command...");
                    try
                    {
                        CommandValidator.HandleCommand(receivedTopic, payload);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Command processing failed: {ex.Message}");
                    }
                }

                await Task.CompletedTask;
            };
        }

        public async void Send(string data, string coolerId, bool retain = false)
        {
            try
            {
                Console.WriteLine($"[LOG] Sending data: {data} (retain: {retain})");
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic($"water_coolers/{coolerId}/readings")
                    .WithPayload(data)
                    .WithRetainFlag(retain)
                    .Build();
                await mqttClient.PublishAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error sending data: {ex.Message}");
            }
        }

        public async void Subscribe(string topic)
        {
            try
            {
                Console.WriteLine($"[LOG] Attempting to subscribe to topic: {topic}");
                var response = await mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(f => { f.WithTopic(topic); })
                    .Build());

                if (response.Items.Any(item =>
                    item.ResultCode == MqttClientSubscribeResultCode.GrantedQoS0 ||
                    item.ResultCode == MqttClientSubscribeResultCode.GrantedQoS1 ||
                    item.ResultCode == MqttClientSubscribeResultCode.GrantedQoS2))
                {
                    Console.WriteLine($"[LOG] Successfully subscribed to topic: {topic}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Subscription to topic {topic} was not granted");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error subscribing to topic {topic}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            mqttClient?.DisconnectAsync().Wait();
            mqttClient?.Dispose();
        }
    }
}