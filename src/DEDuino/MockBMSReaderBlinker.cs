using F4SharedMem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DEDuino
{
    public class MockBMSReaderBlinker: IBMSReader
    {
        public bool IsFalconRunning
        {
            get { return true; }
        }

        public FlightData GetCurrentData()
        {
            FlightData fakeFlightData = new FlightData();
            var lightsOn = false;
            var currentSeconds = DateTime.Now.Second;
            var secondsLastDigit = currentSeconds % 10;

            // in theory this will turn the lights on/off every 5 seconds
            if (secondsLastDigit <5)
            {
                lightsOn = true;
            }
            
            if (lightsOn)
            {
                LightsOn(ref fakeFlightData);
            }
            else
            {
                LightsOff(ref fakeFlightData);
            }
            
            return fakeFlightData;
        }

        private void LightsOff(ref FlightData flightData)
        {
            flightData.lightBits = 0;
            flightData.lightBits2 = 0;
            flightData.lightBits3 = 0;
        }

        private void LightsOn(ref FlightData flightData)
        {
            // this may not do what I expect it to but the intention is to turn on all the light bits
            flightData.lightBits = int.MaxValue;
            flightData.lightBits2 = int.MaxValue;
            flightData.lightBits3 = int.MaxValue;
        }
    }
}
