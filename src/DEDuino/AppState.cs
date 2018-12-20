using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DEDuino
{
    public class AppState
    {
        public AppState(string cautionPanelVersion)
        {
            this.IsClosing = false;
            this.BMS432 = false;
            this.JshepCP = false;
            this.CautionPanelVer = cautionPanelVersion;            
        }

        public bool IsClosing
        {
            get; set;
        }

        public bool BMS432
        {
            get; set;
        }

        public bool JshepCP
        {
            get; set;
        }

        public string CautionPanelVer
        {
            get; set;
        }

        public char SerialBuffer
        {
            get;set;
        }
    }
}
