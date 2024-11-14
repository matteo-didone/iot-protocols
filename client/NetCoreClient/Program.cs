using NetCoreClient.Protocols;
using NetCoreClient.Sensors;
using System.Text.Json;

class Program
{
    static void Main(string[] args)
    {
        // Inizializza i sensori
        var waterFlowSensor = new VirtualWaterFlowSensor();
        var waterTempSensor = new VirtualWaterTempSensor();

        // Inizializza il protocollo MQTT
        var protocol = new Mqtt("localhost:1883");

        try
        {
            while (true)
            {
                // Lettura e invio dati sensore flusso
                var waterFlowData = new
                {
                    coolerId = "cooler_001",
                    measurement = "water_flow",
                    value = waterFlowSensor.WaterFlow(),
                    timestamp = DateTime.UtcNow
                };
                protocol.Send(JsonSerializer.Serialize(waterFlowData));

                // Lettura e invio dati sensore temperatura
                var tempData = new
                {
                    coolerId = "cooler_001",
                    measurement = "water_temperature",
                    value = waterTempSensor.WaterTemperature(),
                    timestamp = DateTime.UtcNow
                };
                protocol.Send(JsonSerializer.Serialize(tempData));

                Console.WriteLine("Readings sent");
                Thread.Sleep(5000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            if (protocol is IDisposable disposableProtocol)
            {
                disposableProtocol.Dispose();
            }
        }
    }
}