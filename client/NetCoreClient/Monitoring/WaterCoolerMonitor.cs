using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NetCoreClient.Protocols;

namespace NetCoreClient.Monitoring
{
    public class StatsInfo
    {
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double LastValue { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    public class WaterCoolerStatus
    {
        public string CoolerId { get; set; } = string.Empty;
        public bool IsPoweredOn { get; set; }
        public double TotalLitersDispensed { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public Dictionary<string, double> LastReadings { get; set; } = new();
        public Dictionary<string, StatsInfo> Stats { get; set; } = new();
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
            await Task.CompletedTask;
        }

        private void HandleAmqpMessage(object? sender, CommandEventArgs e)
        {
            try
            {
                // Processa solo i messaggi che terminano con .response
                if (!e.Topic.EndsWith(".response"))
                {
                    return;
                }

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

                if (e.Topic.EndsWith(".readings.response"))
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
                var timestamp = timestampElement.GetDateTime();

                if (!string.IsNullOrEmpty(measurement))
                {
                    status.LastReadings[measurement] = value;
                    status.LastUpdateTime = timestamp;

                    if (measurement == "water_flow")
                    {
                        if (value > 0)
                        {
                            var previousTotal = status.TotalLitersDispensed;
                            status.TotalLitersDispensed += value;
                            Console.WriteLine($"[LOG] Litri erogati per {coolerId}: {previousTotal:F2}L -> {status.TotalLitersDispensed:F2}L (+" +
                                $"{value:F2}L)");
                        }
                        else
                        {
                            Console.WriteLine($"[LOG] Flusso zero o negativo ignorato: {value:F2}L");
                        }
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
                    var statsInfo = new StatsInfo
                    {
                        Average = stat.Value.GetProperty("average").GetDouble(),
                        Min = stat.Value.GetProperty("min").GetDouble(),
                        Max = stat.Value.GetProperty("max").GetDouble(),
                        LastValue = stat.Value.GetProperty("lastValue").GetDouble(),
                        LastUpdate = DateTime.UtcNow
                    };

                    var measurementType = stat.Name;
                    var oldStats = status.Stats.ContainsKey(measurementType) ? status.Stats[measurementType] : null;

                    if (oldStats != null)
                    {
                        Console.WriteLine($"[LOG] Statistiche {measurementType} per {coolerId}:");
                        if (Math.Abs(oldStats.Average - statsInfo.Average) > 0.01)
                        {
                            Console.WriteLine($"  • Media: {oldStats.Average:F2} -> {statsInfo.Average:F2}");
                        }
                        if (Math.Abs(oldStats.Min - statsInfo.Min) > 0.01)
                        {
                            Console.WriteLine($"  • Min: {oldStats.Min:F2} -> {statsInfo.Min:F2}");
                        }
                        if (Math.Abs(oldStats.Max - statsInfo.Max) > 0.01)
                        {
                            Console.WriteLine($"  • Max: {oldStats.Max:F2} -> {statsInfo.Max:F2}");
                        }
                        if (Math.Abs(oldStats.LastValue - statsInfo.LastValue) > 0.01)
                        {
                            Console.WriteLine($"  • Ultimo valore: {oldStats.LastValue:F2} -> {statsInfo.LastValue:F2}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[LOG] Prime statistiche {measurementType} per {coolerId}:");
                        Console.WriteLine($"  • Media: {statsInfo.Average:F2}");
                        Console.WriteLine($"  • Min: {statsInfo.Min:F2}");
                        Console.WriteLine($"  • Max: {statsInfo.Max:F2}");
                        Console.WriteLine($"  • Ultimo valore: {statsInfo.LastValue:F2}");
                    }

                    status.Stats[measurementType] = statsInfo;
                    status.LastReadings[measurementType] = statsInfo.LastValue;
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
                    if (status.Stats.TryGetValue(reading.Key, out var stats))
                    {
                        Console.WriteLine($"    - Media: {stats.Average:F2}");
                        Console.WriteLine($"    - Min: {stats.Min:F2}");
                        Console.WriteLine($"    - Max: {stats.Max:F2}");
                        Console.WriteLine($"    - Ultimo aggiornamento statistiche: {stats.LastUpdate:HH:mm:ss}");
                    }
                }

                Console.WriteLine($"  • Ultimo aggiornamento: {status.LastUpdateTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine("---------------------------");
            }
        }
    }
}