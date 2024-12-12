using System;
using System.Text.Json;
using System.Threading.Tasks;
using NetCoreClient.Protocols;
using NetCoreClient.Monitoring;
using NetCoreClient.Sensors;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: program <cooler_id>");
            Console.WriteLine("Example: program cooler_001");
            return;
        }

        string coolerId = args[0];
        var protocol = new Amqp("localhost");
        var monitor = new WaterCoolerMonitor(protocol);

        // Inizializza i sensori virtuali
        var waterFlowSensor = new VirtualWaterFlowSensor();
        var waterTempSensor = new VirtualWaterTempSensor();

        try
        {
            await monitor.StartMonitoring();
            Console.WriteLine($"Started monitoring for cooler {coolerId}");

            // Sottoscrivi solo ai topic di risposta specifici per questo cooler
            protocol.Subscribe($"water_coolers.{coolerId}.readings.response");
            protocol.Subscribe($"water_coolers.{coolerId}.data.response");

            while (true)
            {
                try
                {
                    // Invia lettura del flusso d'acqua
                    var flowData = new
                    {
                        coolerId = coolerId,
                        measurement = "water_flow",
                        value = waterFlowSensor.WaterFlow(),
                        timestamp = DateTime.UtcNow
                    };

                    protocol.SendCommand($"water_coolers.{coolerId}.readings",
                        JsonSerializer.Serialize(flowData));

                    // Invia lettura della temperatura
                    var tempData = new
                    {
                        coolerId = coolerId,
                        measurement = "water_temperature",
                        value = waterTempSensor.WaterTemperature(),
                        timestamp = DateTime.UtcNow
                    };

                    protocol.SendCommand($"water_coolers.{coolerId}.readings",
                        JsonSerializer.Serialize(tempData));

                    // Richiedi dati aggiornati
                    protocol.SendCommand($"water_coolers.{coolerId}.data", "{}");

                    // Stampa lo stato corrente
                    monitor.PrintCurrentStatus();

                    // Attendi prima della prossima lettura
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error during sensor reading: {ex.Message}");
                    await Task.Delay(1000); // Breve attesa in caso di errore
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            protocol.Dispose();
        }
    }
}