using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using System.Text.Json;
using NetCoreClient.Commands;

namespace NetCoreClient.Monitoring
{
    public class WaterCoolerStatus
    {
        public string CoolerId { get; set; } = string.Empty;
        public bool IsPoweredOn { get; set; }
        public double TotalLitersDispensed { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    public class WaterCoolerMonitor
    {
        private readonly Dictionary<string, WaterCoolerStatus> _coolerStatuses;
        private readonly IMqttClient _mqttClient;
        private readonly string _topicPrefix = "water_coolers/";
        private readonly string _commandsPrefix = "commands/";

        public WaterCoolerMonitor(IMqttClient mqttClient)
        {
            _coolerStatuses = new Dictionary<string, WaterCoolerStatus>
            {
                { "cooler_001", new WaterCoolerStatus { CoolerId = "cooler_001", IsPoweredOn = false, TotalLitersDispensed = 0 } },
                { "cooler_002", new WaterCoolerStatus { CoolerId = "cooler_002", IsPoweredOn = false, TotalLitersDispensed = 0 } },
                { "cooler_003", new WaterCoolerStatus { CoolerId = "cooler_003", IsPoweredOn = false, TotalLitersDispensed = 0 } },
                { "cooler_004", new WaterCoolerStatus { CoolerId = "cooler_004", IsPoweredOn = false, TotalLitersDispensed = 0 } }
            };
            _mqttClient = mqttClient;

            // Sottoscrivi all'evento di power state changed
            CommandValidator.PowerStateChanged += OnPowerStateChanged;
        }

        private void OnPowerStateChanged(string coolerId, bool powerState)
        {
            if (_coolerStatuses.ContainsKey(coolerId))
            {
                _coolerStatuses[coolerId].IsPoweredOn = powerState;
                Console.WriteLine($"[LOG] Updated power state for {coolerId}: {(powerState ? "ON" : "OFF")}");
            }
        }

        public async Task StartMonitoring()
        {
            await _mqttClient.SubscribeAsync($"{_topicPrefix}+/readings");
            await _mqttClient.SubscribeAsync($"{_commandsPrefix}+/power");

            _mqttClient.ApplicationMessageReceivedAsync += HandleMqttMessage;
        }

        private Task HandleMqttMessage(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            if (topic.StartsWith(_topicPrefix))
            {
                HandleReadings(payload);
            }

            return Task.CompletedTask;
        }

        private void HandleReadings(string payload)
        {
            try
            {
                var reading = JsonSerializer.Deserialize<WaterFlowReading>(payload);
                if (reading == null) return;

                if (!_coolerStatuses.ContainsKey(reading.CoolerId)) return;

                var cooler = _coolerStatuses[reading.CoolerId];

                // Aggiorna i litri solo se la casetta è accesa
                if (cooler.IsPoweredOn && reading.Measurement == "water_flow")
                {
                    // Arrotonda a 2 decimali per una visualizzazione più pulita
                    cooler.TotalLitersDispensed = Math.Round(
                        cooler.TotalLitersDispensed + reading.Value,
                        2
                    );
                    cooler.LastUpdateTime = reading.Timestamp;

                    // Log più dettagliato
                    Console.WriteLine($"[LOG] Erogazione acqua - Casetta {reading.CoolerId}:");
                    Console.WriteLine($"      + {reading.Value:F2}L (Totale: {cooler.TotalLitersDispensed:F2}L)");
                }
                else if (!cooler.IsPoweredOn && reading.Value > 0)
                {
                    Console.WriteLine($"[LOG] Ignorata erogazione per {reading.CoolerId} - Casetta spenta");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ERROR] Errore parsing lettura: {ex.Message}");
            }
        }

        public void PrintCurrentStatus()
        {
            Console.WriteLine("\n=== STATO CASETTE DELL'ACQUA ===");
            foreach (var status in _coolerStatuses.Values)
            {
                Console.WriteLine($"Casetta {status.CoolerId}:");
                Console.WriteLine($"  • Stato: {(status.IsPoweredOn ? "ACCESA" : "SPENTA")}");
                Console.WriteLine($"  • Litri erogati: {status.TotalLitersDispensed:F2}L");
                Console.WriteLine("---------------------------");
            }
        }

        private class WaterFlowReading
        {
            public string CoolerId { get; set; } = string.Empty;
            public string Measurement { get; set; } = string.Empty;
            public double Value { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}