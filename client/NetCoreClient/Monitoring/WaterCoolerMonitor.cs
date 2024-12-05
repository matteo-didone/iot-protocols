using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NetCoreClient.Protocols;

namespace NetCoreClient.Monitoring
{
    public class WaterCoolerStatus
    {
        public string CoolerId { get; set; } = string.Empty;
        public bool IsPoweredOn { get; set; }
        public double TotalLitersDispensed { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public Dictionary<string, double> LastReadings { get; set; } = new();
    }

    public class WaterCoolerMonitor
    {
        private readonly Dictionary<string, WaterCoolerStatus> _coolerStatuses;
        private readonly IProtocolInterface _protocol;

        public WaterCoolerMonitor(IProtocolInterface protocol)
        {
            _coolerStatuses = new Dictionary<string, WaterCoolerStatus>();
            _protocol = protocol;
            _protocol.OnCommandReceived += HandleAmqpMessage;
        }

        public async Task StartMonitoring()
        {
            Console.WriteLine("[LOG] Starting monitoring...");
            _protocol.Subscribe("water_coolers.+.readings.response");
            _protocol.Subscribe("water_coolers.+.data.response");
            _protocol.Subscribe("water_coolers.list.response");
            await Task.CompletedTask;
        }

        private void HandleAmqpMessage(object? sender, CommandEventArgs e)
        {
            try
            {
                Console.WriteLine($"[LOG] Received message on topic: {e.Topic}");
                Console.WriteLine($"[LOG] Message payload: {e.Payload}");

                using var doc = JsonDocument.Parse(e.Payload);
                var root = doc.RootElement;

                if (!root.TryGetProperty("status", out var status) ||
                    status.GetString() != "success")
                {
                    Console.WriteLine("[WARN] Invalid response format or status not success");
                    return;
                }

                // Controllo del topic senza slash
                if (e.Topic == "water_coolers.list.response")
                {
                    HandleListResponse(root);
                }
                else if (e.Topic.EndsWith(".readings.response"))
                {
                    if (root.TryGetProperty("reading", out var reading))
                    {
                        HandleReadingResponse(root);
                    }
                }
                else if (e.Topic.EndsWith(".data.response"))
                {
                    HandleDataResponse(root);
                }
                else
                {
                    Console.WriteLine($"[WARN] Unhandled topic: {e.Topic}");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ERROR] JSON parsing error: {ex.Message}\nPayload: {e.Payload}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Message handling error: {ex.Message}");
            }
        }

        private void HandleListResponse(JsonElement response)
        {
            Console.WriteLine("[LOG] Processing list response");

            if (!response.TryGetProperty("coolers", out var coolersArray))
            {
                Console.WriteLine("[WARN] No coolers array in response");
                return;
            }

            foreach (var coolerElement in coolersArray.EnumerateArray())
            {
                var coolerId = coolerElement.GetString();
                if (!string.IsNullOrEmpty(coolerId))
                {
                    Console.WriteLine($"[LOG] Adding/updating cooler: {coolerId}");
                    EnsureCoolerExists(coolerId);
                }
            }
        }

        private void HandleReadingResponse(JsonElement response)
        {
            if (!response.TryGetProperty("reading", out var reading))
            {
                Console.WriteLine("[WARN] No reading in response");
                return;
            }

            var coolerId = reading.GetProperty("coolerId").GetString();
            if (string.IsNullOrEmpty(coolerId))
            {
                Console.WriteLine("[WARN] No coolerId in reading");
                return;
            }

            Console.WriteLine($"[LOG] Processing reading for cooler: {coolerId}");
            EnsureCoolerExists(coolerId);

            var status = _coolerStatuses[coolerId];
            if (reading.TryGetProperty("measurement", out var measurementElement) &&
                reading.TryGetProperty("value", out var valueElement) &&
                reading.TryGetProperty("timestamp", out var timestampElement))
            {
                var measurement = measurementElement.GetString();
                var value = valueElement.GetDouble();

                if (!string.IsNullOrEmpty(measurement))
                {
                    status.LastReadings[measurement] = value;
                    status.LastUpdateTime = timestampElement.GetDateTime();

                    if (measurement == "water_flow")
                    {
                        status.TotalLitersDispensed += value;
                    }
                    Console.WriteLine($"[LOG] Updated {measurement} for {coolerId}: {value}");
                }
            }
        }

        private void HandleDataResponse(JsonElement response)
        {
            if (!response.TryGetProperty("coolerId", out var coolerIdElement))
            {
                Console.WriteLine("[WARN] No coolerId in data response");
                return;
            }

            var coolerId = coolerIdElement.GetString();
            if (string.IsNullOrEmpty(coolerId))
            {
                Console.WriteLine("[WARN] Invalid coolerId in data response");
                return;
            }

            Console.WriteLine($"[LOG] Processing data for cooler: {coolerId}");
            EnsureCoolerExists(coolerId);

            var status = _coolerStatuses[coolerId];
            if (response.TryGetProperty("stats", out var stats))
            {
                foreach (var stat in stats.EnumerateObject())
                {
                    if (stat.Value.TryGetProperty("lastValue", out var lastValue))
                    {
                        status.LastReadings[stat.Name] = lastValue.GetDouble();
                        Console.WriteLine($"[LOG] Updated stat {stat.Name} for {coolerId}: {lastValue.GetDouble()}");
                    }
                }
            }

            if (response.TryGetProperty("lastUpdate", out var lastUpdate))
            {
                status.LastUpdateTime = lastUpdate.GetDateTime();
            }
        }

        private void EnsureCoolerExists(string coolerId)
        {
            if (!_coolerStatuses.ContainsKey(coolerId))
            {
                _coolerStatuses[coolerId] = new WaterCoolerStatus
                {
                    CoolerId = coolerId,
                    IsPoweredOn = true,
                    LastUpdateTime = DateTime.UtcNow
                };
                Console.WriteLine($"[LOG] Created new status for cooler: {coolerId}");
            }
        }

        public void PrintCurrentStatus()
        {
            Console.WriteLine("\n=== STATO CASETTE DELL'ACQUA ===");
            if (_coolerStatuses.Count == 0)
            {
                Console.WriteLine("Nessuna casetta registrata");
                return;
            }

            foreach (var status in _coolerStatuses.Values)
            {
                Console.WriteLine($"\nCasetta {status.CoolerId}:");
                Console.WriteLine($"  • Stato: {(status.IsPoweredOn ? "ACCESA" : "SPENTA")}");
                Console.WriteLine($"  • Totale litri erogati: {status.TotalLitersDispensed:F2}L");

                foreach (var reading in status.LastReadings)
                {
                    Console.WriteLine($"  • Ultima lettura {reading.Key}: {reading.Value:F2}");
                }

                Console.WriteLine($"  • Ultimo aggiornamento: {status.LastUpdateTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine("---------------------------");
            }
        }
    }
}