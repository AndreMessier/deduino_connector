using System.IO.Ports;

namespace DEDuino
{
    public interface ISerialComm
    {
        bool IsOpen { get; }

        string[] CheckPorts();
        bool CloseConnection(ref AppState appState);
        bool OpenConnection(string portName, ref AppState appState, int baudRate, SerialDataReceivedEventHandler receivedEventHandler);
        void Write(byte[] buffer, int offset, int count);
        void DiscardInBuffer();
        void DiscardOutBuffer();
    }
}