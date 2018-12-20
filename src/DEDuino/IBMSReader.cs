using F4SharedMem;

namespace DEDuino
{
    public interface IBMSReader
    {
        bool IsFalconRunning { get; }

        FlightData GetCurrentData();
    }
}