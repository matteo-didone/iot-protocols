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
            var factory = new ConnectionFactory() 
            { 
                HostName = hostName,
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = true
            };

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
                properties.ContentType = "application/json";

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
                throw;
            }
        }

        public void SendCommand(string topic, string payload)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(payload);
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: topic,
                    basicProperties: properties,
                    body: body);

                Console.WriteLine($"[LOG] Sent command to {topic}: {payload}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error sending command: {ex.Message}");
                throw;
            }
        }

        public void Subscribe(string topic)
        {
            try
            {
                var queueName = $"queue_{Guid.NewGuid()}";

                // Declare a queue with auto-delete
                var queueDeclareOk = _channel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: true,
                    arguments: null);

                Console.WriteLine($"[LOG] Binding queue {queueName} to exchange {_exchangeName} with routing key {topic}");

                // Bind the queue to the exchange with the original topic pattern
                _channel.QueueBind(
                    queue: queueName,
                    exchange: _exchangeName,
                    routingKey: topic);

                // Set up consumer with manual ack
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        OnCommandReceived?.Invoke(this, new CommandEventArgs(
                            ea.RoutingKey,
                            message
                        ));

                        // Acknowledge the message
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Error processing message: {ex.Message}");
                        // Negative acknowledge the message
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                // Enable manual ack
                _channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);

                _queueBindings[topic] = queueName;
                Console.WriteLine($"[LOG] Subscribed to topic: {topic} with queue {queueName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error subscribing to topic {topic}: {ex.Message}");
                throw;
            }
        }

        public void Unsubscribe(string topic)
        {
            try
            {
                if (_queueBindings.TryGetValue(topic, out string? queueName) && queueName != null)
                {
                    _channel.QueueUnbind(
                        queue: queueName,
                        exchange: _exchangeName,
                        routingKey: topic);

                    _channel.QueueDelete(queueName);
                    _queueBindings.Remove(topic);
                    Console.WriteLine($"[LOG] Unsubscribed from topic: {topic}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error unsubscribing from topic {topic}: {ex.Message}");
                throw;
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

                _channel?.Close();
                _channel?.Dispose();
                _connection?.Close();
                _connection?.Dispose();

                Console.WriteLine("[LOG] AMQP connection closed and resources disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error during disposal: {ex.Message}");
                throw;
            }
        }
    }
}