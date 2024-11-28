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
        var protocol = new Mqtt("localhost");
        var monitor = new WaterCoolerMonitor(protocol.GetMqttClient());
        var random = new Random();

        // Sottoscrivi all'evento di richiesta status
        protocol.RequestStatusUpdate += () => monitor.PrintCurrentStatus();
        
        // Lista delle casette
        var coolerIds = new List<string>
        {
            "cooler_001",
            "cooler_002",
            "cooler_003",
            "cooler_004"
        };

        try
        {
            // Avvia il monitoraggio
            await monitor.StartMonitoring();
            Console.WriteLine("Monitoring started...");

            // Sottoscrivi ai comandi per tutte le casette
            foreach (var coolerId in coolerIds)
            {
                protocol.Subscribe($"commands/{coolerId}/#");
            }
            // Sottoscrivi al comando di status
            protocol.Subscribe("commands/status");

            while (true)
            {
                // Simula l'invio di dati da tutte le casette
                foreach (var coolerId in coolerIds)
                {
                    var data = new
                    {
                        coolerId = coolerId,
                        measurement = "water_flow",
                        value = random.Next(0, 10),
                        timestamp = DateTime.UtcNow
                    };

                    protocol.Send(JsonSerializer.Serialize(data), coolerId);
                }

                // Stampa lo stato di tutte le casette
                Console.WriteLine("\nPer testare i comandi, usa mosquitto_pub. Esempi:");
                Console.WriteLine("Accensione: mosquitto_pub -h localhost -p 1883 -t \"commands/cooler_001/power\" -m '{\"action\": \"power\", \"state\": true}'");
                Console.WriteLine("Spegnimento: mosquitto_pub -h localhost -p 1883 -t \"commands/cooler_001/power\" -m '{\"action\": \"power\", \"state\": false}'");
                Console.WriteLine("Status: mosquitto_pub -h localhost -p 1883 -t \"commands/status\" -m \"show\"");
                
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