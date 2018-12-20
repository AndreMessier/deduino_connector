using F4SharedMem;

namespace DEDuino
{
    public class BMSReaderWrapper : IBMSReader
    {
        private F4SharedMem.Reader BMSreader;

        public BMSReaderWrapper(Reader BMSreader)
        {
            this.BMSreader = BMSreader;
        }

        public bool IsFalconRunning
        {
            get { return BMSreader.IsFalconRunning; }
        }
        
        public FlightData GetCurrentData()
        {
            return BMSreader.GetCurrentData();
        }

    }
}
