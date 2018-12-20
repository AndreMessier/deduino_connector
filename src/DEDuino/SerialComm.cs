using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DEDuino
{
    public class SerialComm : ISerialComm
    {

        private SerialPort serialDevice = new SerialPort();

        /// <summary>
        /// scans the computer for available COM ports
        /// </summary>
        /// <returns>array of strings containing the port names</returns>
        public string[] CheckPorts() //Function scans computer for available COM ports and return
        {
            return SerialPort.GetPortNames();
        }

        public bool CloseConnection(ref AppState appState)
        {
            appState.IsClosing = true; // set "closing flag"
            Thread.Sleep(200); // wait for all interrupts for finish processing
            serialDevice.Close(); // close the connection
            return true;
        }

        public bool OpenConnection(string portName, ref AppState appState, int baudRate, SerialDataReceivedEventHandler receivedEventHandler)
        {
            serialDevice.PortName = portName;
            serialDevice.RtsEnable = true; // set RTS flag
            serialDevice.Handshake = Handshake.None; // Disable handshake                                                  
            serialDevice.BaudRate = baudRate;
            serialDevice.DataReceived += new SerialDataReceivedEventHandler(receivedEventHandler); // Setup serial interrup routine - that is what will actually do the work
            appState.IsClosing = false; // set closing flag to off
            serialDevice.Open(); // Open Serial connection
            if (serialDevice.IsOpen) // if succeded
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            serialDevice.Write(buffer, offset, count);
        }

        public bool IsOpen
        {
            get { return serialDevice.IsOpen; }
        }

        public void DiscardInBuffer()
        {
            serialDevice.DiscardInBuffer();
        }

        public void DiscardOutBuffer()
        {
            serialDevice.DiscardOutBuffer();
        }
    }
}
