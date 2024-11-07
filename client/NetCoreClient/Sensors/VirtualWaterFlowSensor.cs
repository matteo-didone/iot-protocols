using NetCoreClient.ValueObjects;
using System.Text.Json;

namespace NetCoreClient.Sensors
{
    class VirtualWaterFlowSensor : IWaterFlowSensorInterface, ISensorInterface
    {
        private readonly Random Random;
        public VirtualWaterFlowSensor()
        {
            Random = new Random();
        }

        public int WaterFlow()
        {
            return new WaterFlow(Random.Next(10)).Value;
        }

        public string ToJson()
        {
            var measurement = new
            {
                measurement = "water_flow",
                value = WaterFlow(),
                timestamp = DateTime.UtcNow.ToString("o")
            };
            return JsonSerializer.Serialize(measurement);
        }
    }
}