using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetCoreClient.Protocols;
using System.Collections.Generic;
using NetCoreClient.Monitoring;

class Program
{
    static async Task Main(string[] args)
    {
        var protocol = new Amqp("localhost");
        var monitor = new WaterCoolerMonitor(protocol);
        var random = new Random();

        var coolerIds = new List<string>
        {
            "cooler_001",
            "cooler_002",
            "cooler_003",
            "cooler_004"
        };

        try
        {
            await monitor.StartMonitoring();
            Console.WriteLine("Monitoring started...");

            while (true)
            {
                // Richiedi lista delle casette
                protocol.SendCommand("water_coolers.list", "{}");

                // Invia i dati simulati per ogni casetta
                foreach (var coolerId in coolerIds)
                {
                    var data = new
                    {
                        coolerId = coolerId,
                        measurement = "water_flow",
                        value = random.NextDouble() * 0.4 + 0.1,
                        timestamp = DateTime.UtcNow
                    };

                    protocol.SendCommand($"water_coolers.{coolerId}.readings",
                        JsonSerializer.Serialize(data));

                    protocol.SendCommand($"water_coolers.{coolerId}.data", "{}");
                }

                monitor.PrintCurrentStatus();
                Thread.Sleep(5000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
        finally
        {
            protocol.Dispose();
        }
    }
}