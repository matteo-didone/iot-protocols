namespace NetCoreClient.ValueObjects
{
    internal class WaterFlow
    {
        public int Value { get; private set; }
        public WaterFlow(int value)
        {
            this.Value = value;
        }
    }
}