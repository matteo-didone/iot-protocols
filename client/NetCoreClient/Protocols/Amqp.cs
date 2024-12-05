using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Collections.Generic;

namespace NetCoreClient.Protocols
{
    public class Amqp : IProtocolInterface
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _exchangeName = "water_coolers";
        private readonly Dictionary<string, string> _queueBindings = new();

        public event EventHandler<CommandEventArgs>? OnCommandReceived;

        public Amqp(string hostName)
        {
            var factory = new ConnectionFactory() { HostName = hostName };

            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare exchange
                _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, true);

                Console.WriteLine("[LOG] Connected to AMQP broker successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to connect to AMQP broker: {ex.Message}");
                throw;
            }
        }

        public void Send(string data)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(data);
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: "readings",
                    basicProperties: properties,
                    body: body);

                Console.WriteLine($"[LOG] Sent data: {data}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error sending data: {ex.Message}");
            }
        }

        public void SendCommand(string topic, string payload)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(payload);
                var routingKey = topic;

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: routingKey,
                    basicProperties: null,
                    body: body);

                Console.WriteLine($"[LOG] Sent command to {routingKey}: {payload}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error sending command: {ex.Message}");
            }
        }

        public void Subscribe(string topic)
        {
            try
            {
                var routingKey = topic;
                var queueName = $"queue_{Guid.NewGuid()}";

                // Declare a queue
                _channel.QueueDeclare(queueName, true, false, true);

                // Per il pattern matching dei topic, sostituiamo + con *
                if (routingKey.Contains("+"))
                {
                    routingKey = routingKey.Replace("+", "*");
                }

                Console.WriteLine($"[LOG] Binding queue {queueName} to exchange {_exchangeName} with routing key {routingKey}");

                // Bind the queue to the exchange
                _channel.QueueBind(queueName, _exchangeName, routingKey);

                // Set up consumer
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    // Notifica il messaggio con il routing key originale
                    OnCommandReceived?.Invoke(this, new CommandEventArgs(
                        ea.RoutingKey,
                        message
                    ));
                };

                _channel.BasicConsume(
                    queue: queueName,
                    autoAck: true,
                    consumer: consumer);

                _queueBindings[topic] = queueName;
                Console.WriteLine($"[LOG] Subscribed to topic: {topic} with queue {queueName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error subscribing to topic {topic}: {ex.Message}");
            }
        }

        public void Unsubscribe(string topic)
        {
            try
            {
                if (_queueBindings.TryGetValue(topic, out string? queueName) && queueName != null)
                {
                    _channel.QueueDelete(queueName);
                    _queueBindings.Remove(topic);
                    Console.WriteLine($"[LOG] Unsubscribed from topic: {topic}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error unsubscribing from topic {topic}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                foreach (var queueName in _queueBindings.Values)
                {
                    try
                    {
                        _channel?.QueueDelete(queueName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Error deleting queue {queueName}: {ex.Message}");
                    }
                }

                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error during disposal: {ex.Message}");
            }
        }
    }
}