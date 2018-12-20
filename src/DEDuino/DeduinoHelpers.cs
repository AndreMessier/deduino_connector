using F4SharedMem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DEDuino
{
    public static class DeduinoHelpers
    {
        public static bool CheckLight(F4SharedMem.Headers.LightBits datamask, FlightData BMSdata)
        {
            if ((BMSdata.lightBits & (Int32)datamask) == (Int32)datamask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool CheckLight(F4SharedMem.Headers.LightBits2 datamask, FlightData BMSdata)
        {
            if ((BMSdata.lightBits2 & (Int32)datamask) == (Int32)datamask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool CheckLight(F4SharedMem.Headers.LightBits3 datamask, FlightData BMSdata)
        {
            if ((BMSdata.lightBits3 & (Int32)datamask) == (Int32)datamask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool CheckLight(F4SharedMem.Headers.HsiBits datamask, FlightData BMSdata)
        {
            if ((BMSdata.hsiBits & (Int32)datamask) == (Int32)datamask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool CheckLight(F4SharedMem.Headers.PowerBits datamask, FlightData BMSdata)
        {
            if ((BMSdata.powerBits & (Int32)datamask) == (Int32)datamask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
