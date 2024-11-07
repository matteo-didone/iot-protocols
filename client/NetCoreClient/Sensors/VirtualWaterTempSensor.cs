using NetCoreClient.ValueObjects;
using System.Text.Json;

namespace NetCoreClient.Sensors
{
    class VirtualWaterTempSensor : IWaterTempSensorInterface, ISensorInterface
    {
        private readonly Random Random;

        public VirtualWaterTempSensor()
        {
            Random = new Random();
        }

        public int WaterTemperature()
        {
            return new WaterTemperature(Random.Next(20)).Value;
        }

        public string ToJson()
        {
            var measurement = new
            {
                measurement = "water_temperature",
                value = WaterTemperature(),
                timestamp = DateTime.UtcNow.ToString("o")  // ISO 8601 format
            };

            return JsonSerializer.Serialize(measurement);
        }
    }
}