using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NetCoreClient.Protocols;

namespace NetCoreClient.Protocols
{
    public class Amqp : ProtocolInterface, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _exchangeName = "water_coolers";

        public event EventHandler<CommandEventArgs> OnCommandReceived;

        public Amqp(string hostName)
        {
            var factory = new ConnectionFactory() { HostName = hostName };

            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

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
                Console.WriteLine($"[LOG] Sending data: {data}");
                var body = Encoding.UTF8.GetBytes(data);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: "readings",
                    basicProperties: properties,
                    body: body);
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
                var routingKey = topic.Replace("/", ".");

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: routingKey,
                    basicProperties: null,
                    body: body);
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
                Console.WriteLine($"[LOG] Attempting to subscribe to topic: {topic}");

                var queueName = _channel.QueueDeclare().QueueName;
                var routingKey = topic.Replace("/", ".").Replace("#", "*");

                _channel.QueueBind(queueName, _exchangeName, routingKey);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var receivedTopic = ea.RoutingKey.Replace(".", "/");

                    OnCommandReceived?.Invoke(this, new CommandEventArgs(
                        receivedTopic,
                        message
                    ));
                };

                _channel.BasicConsume(
                    queue: queueName,
                    autoAck: true,
                    consumer: consumer);
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
                // In AMQP, possiamo eliminare la coda o rimuovere il binding
                // Per semplicità, qui non facciamo nulla dato che le code sono
                // esclusive e verranno eliminate automaticamente alla chiusura
                Console.WriteLine($"[LOG] Unsubscribe from topic: {topic}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error unsubscribing from topic {topic}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}