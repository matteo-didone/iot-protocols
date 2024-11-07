using NetCoreClient.Sensors;
using NetCoreClient.Protocols;

// define sensors
List<ISensorInterface> sensors = new();
sensors.Add(new VirtualWaterTempSensor());
sensors.Add(new VirtualWaterFlowSensor());  // Aggiunto il nuovo sensore

// define protocol
ProtocolInterface protocol = new Http("http://localhost:8011/water_coolers/123");

// send data to server
while (true)
{
    foreach (ISensorInterface sensor in sensors)
    {
        var sensorValue = sensor.ToJson();
        protocol.Send(sensorValue);
        Console.WriteLine("Data sent: " + sensorValue);
        Thread.Sleep(1000);
    }
}