
using System;
using System.Text.Json;
using System.Threading;
using NetCoreClient.Protocols;

class Program
{
    static void Main(string[] args)
    {
        var protocol = new Mqtt("localhost");

        try
        {
            while (true)
            {
                var data = new
                {
                    coolerId = "cooler_001",
                    measurement = "water_flow",
                    value = new Random().Next(0, 10),
                    timestamp = DateTime.UtcNow
                };

                protocol.Send(JsonSerializer.Serialize(data));
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
