using F4SharedMem;
using F4SharedMem.Headers;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DEDuino
{
    public class DedFunctions
    {
        private ISerialComm serialComm;
        private AppState appState;
        private IBMSReader BMSreader;
        private FlightData BMSdata;

        public DedFunctions(ISerialComm serialComm, ref AppState appState, ref IBMSReader BMSreader, ref FlightData flightData)
        {
            this.serialComm = serialComm;
            this.appState = appState;
            this.BMSreader = BMSreader;
            this.BMSdata = flightData;
        }

        /// <summary>
        /// Scan the ports and populate the ComboBox with options
        /// </summary>
        /// <param name="comboBox">The combobox to load the list of ports into</param>
        public void updateAvailablePorts(ref ComboBox comboBox) //Function scans computer for available COM ports and puts them into the dropdown box for selection
        {
            var AvailablePorts = serialComm.CheckPorts(); // put available ports into variable
            comboBox.DataSource = AvailablePorts; //insert into drop box
        }


        /// <summary>
        /// This functions handles opening and closing of serial connections
        /// </summary>
        /// <param name="appState">reference to a number of variables that need to be maintained</param>
        /// <param name="isUno">If the board is an Uno we need to set specific options</param>
        /// <param name="comPort">The name of the com port to initialize</param>
        /// <param name="toolStripStatus">A reference to the toolstrip so we can pass status value back</param>
        /// <returns>true if successful.  false otherwise.</returns>
        public bool SerialInit(ref AppState appState, bool isUno, string comPort, ref ToolStripStatusLabel toolStripStatus)
        {
            if (serialComm.IsOpen) //if serial connection is active
            {
                #region SerialDisconnectLogic
                try
                {
                    return serialComm.CloseConnection(ref appState);
                }
                catch (InvalidCastException e) // if exception occurs
                {
                    return false;
                }
                #endregion
            }
            else // if serial connection is not currently open
            {
                if (!string.IsNullOrEmpty(comPort)) // make sure COM port is selected once more (better safe then sorry)
                {
                    #region SerialConnectLogic
                    try
                    {
                        var baudRate = Constants.BAUDRATE * Constants.BAUDRATE_MULTIPLIER;
                        if (isUno) // if We use Arduino Uno (or similar snail)
                        {
                            baudRate = Constants.BAUDRATE * Constants.UNO_BAUDRATE_MULTIPLIER; // Set baud rate to "Uno" speed - because it's slow as F#@$                        
                        }

                        var isOpen = serialComm.OpenConnection(comPort, ref appState, baudRate, dedDevice_DataReceived);
                        if (isOpen) // if succeded
                        {
                            initVars(); //initiallize the Falcon variables
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    catch (InvalidCastException e) //if try fails.
                    {
                        return false;
                    }
                    #endregion
                }
                else // no device is selected
                {
                    toolStripStatus.Text = "No Device Selected";
                    toolStripStatus.BackColor = Color.Orange;
                    return false;
                }
            }
        }

        /// <summary>
        /// Initiallize the required bit variables
        /// </summary>
        public void initVars()
        {
            //hsiBits = new BitArray((byte[])BitConverter.GetBytes(BMSdata.hsiBits));
            //lightBits = new BitArray((byte[])BitConverter.GetBytes(BMSdata.lightBits));
            //lightBits2 = new BitArray((byte[])BitConverter.GetBytes(BMSdata.lightBits2));
            //lightBits3 = new BitArray((byte[])BitConverter.GetBytes(BMSdata.lightBits3));
        }

        /// <summary>
        /// Update sharedmem 
        /// </summary>
        public void FalconUpdate()
        {
            if (BMSreader.IsFalconRunning) //if falcon is running
            {
                BMSdata = BMSreader.GetCurrentData(); // get the current shared mem
                initVars(); // update variables
            }
        }

        /// <summary>
        /// Writes ths line to the serial device
        /// </summary>
        /// <param name="sendThis">string to send</param>
        /// <param name="length">number of characters in the string</param>
        private void sendLine(string sendThis, int length)
        {
            if (sendThis.Length < length)
            {
                length = sendThis.Length;
            }
            byte[] sendBytes = Encoding.GetEncoding(1252).GetBytes(sendThis);
            serialComm.Write(sendBytes, 0, length);
        }

        /// <summary>
        /// Writes bytes to the serial device
        /// </summary>
        /// <param name="sendThis">byte array to send</param>
        /// <param name="length">number of bytes to send</param>
        private void sendBytes(byte[] sendThis, int length)
        {
            serialComm.Write(sendThis, 0, length);
        }

        public string FuelFlowConvert(float FuelFlow)
        {
            return (Math.Round(Convert.ToDecimal(FuelFlow) / 10) * 10).ToString();
        }

        /// <summary>
        /// This the the Heavy lifter this is the serial interrup function
        /// Function recives incoming data 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void dedDevice_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            byte[] blankByte = new byte[1];

            if (!appState.IsClosing)// if we are not in a closing state to your thing
            {
                #region DataProcessingLogic
                int buffersize = sp.BytesToRead;
                SpinWait sw = new SpinWait();
                //Debug.Print("-----");
                //Debug.Print("buff: " + buffersize.ToString());
                if (buffersize == 0)
                {
                    return;
                }
                char[] s = new char[buffersize];
                for (short i = 0; i < s.Length; i++)
                {
                    s[i] = (char)sp.ReadByte();
                }

                byte[] ResponseByte = new byte[1];
                bool PowerOn;
                if (appState.BMS432)
                {
                    PowerOn = BMSreader.IsFalconRunning;
                }
                else
                {
                    PowerOn = DeduinoHelpers.CheckLight(HsiBits.Flying, BMSdata); // test isFlying bit - if Falcon is in 3D world - this is true
                }

                char mode; // define "mode" variable
                ushort LineNum;
                #region mode_logic
                if (s.Length == 1)
                {
                    if (char.IsNumber(s[0]))
                    {
                        try
                        {
                            //Debug.Print("Number only");
                            mode = appState.SerialBuffer;
                            LineNum = ushort.Parse(s[0].ToString());
                        }
                        catch
                        {
                            return;
                        }
                    }
                    else
                    {
                        try
                        {
                            //Debug.Print("letter only");
                            mode = s[0]; // mode is the first character from the serial string
                            appState.SerialBuffer = mode;
                            LineNum = 255;
                            FalconUpdate();
                        }
                        catch
                        {
                            return;
                        }
                    }
                }
                else
                {
                    try
                    {
                        mode = s[0]; // mode is the first character from the serial string
                        if (char.IsNumber(s[1]))
                        {
                            LineNum = ushort.Parse(s[1].ToString());
                            //Debug.Print("two bytes - with number");
                        }
                        else
                        {
                            //Debug.Print("two bytes - no number");
                            LineNum = 255;
                        }
                    }
                    catch
                    {
                        return;
                    }
                }
                //Debug.Print("buff: " + new string (s));
                //Debug.Print("Mode: " + mode);
                //Debug.Print("Line: " + LineNum);
                if (LineNum == 0)
                {
                    FalconUpdate();
                }
                #endregion

                switch (mode) // Get the party started.
                {
                    #region DoWork
                    case 'R': // Recive RDY call from arduino
                        //Debug.Print("buff: " + buffersize.ToString());
                        serialComm.DiscardInBuffer();
                        sendLine("G", 1); // if caught sent back GO command
                        serialComm.DiscardOutBuffer();
                        //MessageBox.Show("R");
                        break; // exit the interrupt
                    case 'U': // Recive UPDATE commant from Arduino - This is not used, but retained for bawards compatibility
                        sendLine("k", 2); // if caught sent back GO command
                        break; // exit the interrupt                 
                    case 'D': //Revice DED request from Arduino - requers reciving D and Number of line
                        #region DED_legacy
                        if (PowerOn && LineNum >= 0 && LineNum < 5)
                        {
                            if (BMSdata.DEDLines != null)
                            {
                                sendLine(NormalizeLine(BMSdata.DEDLines[LineNum], BMSdata.Invert[LineNum]).ToString().PadRight(25, ' '), 25);
                            }
                            else
                            {
                                sendLine(" ".PadRight(25, ' '), 25);

                            }
                        }
                        break;
                    #endregion
                    case 'd': //Revice DED request from Arduino - requers reciving D and Number of line
                        #region DED
                        appState.SerialBuffer = 'd';
                        if (LineNum == 255)
                        {
                            break;
                        }
                        if (PowerOn)
                        {
                            if (BMSdata.DEDLines != null)
                            {
                                sendBytes(NormalizeLine(BMSdata.DEDLines[LineNum], BMSdata.Invert[LineNum]), 24);
                            }
                            else
                            {
                                sendLine(" ".PadRight(24, ' '), 24);
                            }
                        }
                        else
                        {
                            if (LineNum == 2)
                            {
                                sendLine("FALCON NOT READY...".PadRight(24, ' '), 24);
                            }
                            else
                            {
                                sendLine(" ".PadRight(24, ' '), 24);
                            }
                        }
                        break;
                    #endregion
                    case 'P':
                        #region PFL_Legacy
                        if (PowerOn && LineNum >= 0 && LineNum < 5)
                        {
                            if (BMSdata.PFLLines != null)
                            {
                                sendLine(NormalizeLine(BMSdata.PFLLines[LineNum], BMSdata.PFLInvert[LineNum]).ToString().PadRight(25, ' '), 25);
                            }
                            else
                            {
                                sendLine(" ".PadRight(25, ' '), 25);

                            }
                        }
                        break;
                    #endregion
                    case 'p':
                        #region PFL
                        appState.SerialBuffer = 'p';
                        if (LineNum == 255)
                        {
                            break;
                        }
                        if (PowerOn)
                        {
                            if (BMSdata.PFLLines != null)
                            {
                                sendBytes(NormalizeLine(BMSdata.PFLLines[LineNum], BMSdata.PFLInvert[LineNum]), 24);
                            }
                            else
                            {
                                sendLine(" ".PadRight(24, ' '), 24);
                            }
                        }
                        else
                        {
                            if (LineNum == 2)
                            {
                                sendLine("FALCON NOT READY...".PadRight(24, ' '), 24);
                            }
                            else
                            {
                                sendLine(" ".PadRight(24, ' '), 24);
                            }
                        }
                        break;
                    #endregion
                    case 'M':
                        #region CMDS
                        appState.SerialBuffer = 'M';
                        if (LineNum == 255)
                        {
                            break;
                        }
                        if (PowerOn)
                        {
                            sendLine(cmdsMakeLine((short)LineNum).PadRight(24, ' '), 24);
                        }
                        else
                        {
                            sendLine(" ".PadRight(24, ' '), 24);
                        }

                        break;
                    #endregion
                    case 'F':
                        #region FuelFlow

                        if (PowerOn)
                        {
                            if (BMSdata.fuelFlow2 != null)
                            {
                                sendLine(FuelFlowConvert((BMSdata.fuelFlow + BMSdata.fuelFlow2)).PadLeft(5, '0'), 5);

                            }
                            else
                            {
                                sendLine(FuelFlowConvert(BMSdata.fuelFlow).PadLeft(5, '0'), 5);
                            }
                        }
                        else
                        {
                            sendLine("0".PadRight(5, '0'), 5);
                        }
                        break;
                    #endregion
                    case 'A':
                        #region Indexers
                        blankByte[0] = (byte)0;
                        if (PowerOn)
                        {
                            sendBytes(MakeAoaLight(), 1);
                        }
                        else
                        {
                            sendBytes(blankByte, 1);
                        }
                        break;
                    #endregion
                    case 'C':
                        #region CautionPanel
                        blankByte[0] = (byte)0;
                        if (PowerOn)
                        {
                            sendBytes(MakeCautionPanel(appState.CautionPanelVer), 5); // types are "old" and "new", default is "new"                          
                        }
                        else
                        {
                            sendBytes(BitConverter.GetBytes(uint.MinValue), 4);
                            sendBytes(blankByte, 1);
                        }
                        break;
                    #endregion
                    case 'G':
                        #region glareshield
                        blankByte[0] = (byte)0;
                        if (PowerOn)
                        {
                            sendBytes(MakeGlareShield(), 2);
                        }
                        else
                        {
                            // send two blank bytes
                            sendBytes(BitConverter.GetBytes(uint.MinValue), 2);

                        }
                        break;
                    #endregion
                    case 'T':
                        #region TWP
                        sendBytes(blankByte, 1);
                        break;
                    #endregion
                    case 'E':
                        #region Engine
                        if (PowerOn)
                        {
                            // senging out engine data by guage - top to bottom
                            sendBytes(BitConverter.GetBytes(BMSdata.oilPressure), 1);
                            sendBytes(BitConverter.GetBytes(BMSdata.nozzlePos), 1);
                            sendBytes(BitConverter.GetBytes(BMSdata.rpm), 1);
                            sendBytes(BitConverter.GetBytes(BMSdata.ftit), 2);

                        }
                        else
                        {
                            sendBytes(BitConverter.GetBytes(uint.MinValue), 5);

                        }
                        break;
                    #endregion
                    case 'S':
                        #region Speedbreaks
                        if (PowerOn)
                        {
                            sendBytes(MakeSpeedbreaks(), 1);
                        }
                        else
                        {
                            sendBytes(BitConverter.GetBytes('1'), 1); // send inop
                        }
                        break;
                        #endregion

                }
                #endregion
                #endregion
                //dedDevice.DiscardInBuffer();
                //dedDevice.DiscardOutBuffer();
            }
        }

        private bool CheckLight(object flying)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This function yanks out the Indexer bits and returns a byte with the 6 bits (and 2 spacers)
        /// </summary>
        /// <returns></returns>
        private byte[] MakeAoaLight()
        {
            BitArray mapping = new BitArray(8, false);

            mapping[7] = DeduinoHelpers.CheckLight(LightBits.RefuelDSC, BMSdata); // //RefuelDSC
            mapping[6] = DeduinoHelpers.CheckLight(LightBits.RefuelAR, BMSdata); //RefuelAR
            mapping[5] = DeduinoHelpers.CheckLight(LightBits.RefuelRDY, BMSdata); //RefuelRDY
            mapping[4] = false; // blank
            mapping[3] = false; // blank
            mapping[2] = DeduinoHelpers.CheckLight(LightBits.AOABelow, BMSdata); // AOABelow          
            mapping[1] = DeduinoHelpers.CheckLight(LightBits.AOAOn, BMSdata); //AOAOn
            mapping[0] = DeduinoHelpers.CheckLight(LightBits.AOAAbove, BMSdata); //AOAAbove
            byte[] result = new byte[1];
            mapping.CopyTo(result, 0);
            return result;
        }

        /// <summary>
        /// this function takes one string argument "new" or "old" and returns an 5 byte array of light bits acording to the selected layout of the Caution panel
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        private byte[] MakeCautionPanel(string version = "new")
        {
            BitArray mapping = new BitArray(40, false);
            byte[] result = new byte[mapping.Length];
            if (!DeduinoHelpers.CheckLight(LightBits.AllLampBitsOn, BMSdata) && !DeduinoHelpers.CheckLight(LightBits2.AllLampBits2On, BMSdata)) //  check if all the lamp bits on LB1 are up. pretty much will only happen when you check lights.  
            { //if "false" we are not in lightcheck - run logic
                switch (version)
                {
                    case "new":
                        #region newCautionPanel
                        /// left row (bottom to top)
                        mapping[31] = DeduinoHelpers.CheckLight(LightBits2.AftFuelLow, BMSdata); // AFT FUEL LOW
                        mapping[30] = DeduinoHelpers.CheckLight(LightBits2.FwdFuelLow, BMSdata); // FWD FUEL LOW
                        mapping[29] = false; // ATF NOT ENGAGED
                        mapping[28] = DeduinoHelpers.CheckLight(LightBits.CONFIG, BMSdata); // STORES CONFIG
                        mapping[27] = DeduinoHelpers.CheckLight(LightBits3.cadc, BMSdata); // CADC
                        mapping[26] = DeduinoHelpers.CheckLight(LightBits2.PROBEHEAT, BMSdata); // PROBE HEAT
                        mapping[25] = DeduinoHelpers.CheckLight(LightBits3.Elec_Fault, BMSdata); // ELEC SYS
                        mapping[24] = DeduinoHelpers.CheckLight(LightBits.FLCS, BMSdata); // FLCS FAULT
                        /// mid left row (bottom to top)
                        mapping[23] = false; //blank
                        mapping[22] = DeduinoHelpers.CheckLight(LightBits2.BUC, BMSdata); //BUC
                        mapping[21] = false; // EEC
                        mapping[20] = DeduinoHelpers.CheckLight(LightBits.Overheat, BMSdata); // OVERHEAT
                        mapping[19] = false; // INLET ICING
                        mapping[18] = DeduinoHelpers.CheckLight(LightBits2.FUEL_OIL_HOT, BMSdata); // FUEL OIL HOT
                        mapping[17] = DeduinoHelpers.CheckLight(LightBits2.SEC, BMSdata); // SEC
                        mapping[16] = DeduinoHelpers.CheckLight(LightBits.EngineFault, BMSdata); // ENGINE FAULT
                        /// mid right row (bottom to top)
                        mapping[15] = false;  //blank
                        mapping[14] = false;  //blank
                        mapping[13] = false; //blank
                        mapping[12] = false; // nuclear
                        mapping[11] = DeduinoHelpers.CheckLight(LightBits.IFF, BMSdata); // IFF
                        mapping[10] = DeduinoHelpers.CheckLight(LightBits.RadarAlt, BMSdata); // Radar ALT
                        mapping[9] = DeduinoHelpers.CheckLight(LightBits.EQUIP_HOT, BMSdata); // EQUIP HOT
                        mapping[8] = DeduinoHelpers.CheckLight(LightBits.Avionics, BMSdata); // Avionics Fault
                        /// right row (bottom to top)
                        mapping[7] = false; //blank
                        mapping[6] = false; //blank
                        mapping[5] = DeduinoHelpers.CheckLight(LightBits.CabinPress, BMSdata); // Cabin Press
                        mapping[4] = DeduinoHelpers.CheckLight(LightBits2.OXY_LOW, BMSdata); // Oxy_Low
                        mapping[3] = DeduinoHelpers.CheckLight(LightBits.Hook, BMSdata); // hook
                        mapping[2] = DeduinoHelpers.CheckLight(LightBits2.ANTI_SKID, BMSdata); // anti-skid
                        mapping[1] = DeduinoHelpers.CheckLight(LightBits.NWSFail, BMSdata); // NWS fail
                        mapping[0] = DeduinoHelpers.CheckLight(LightBits2.SEAT_ARM, BMSdata); // Seat not armed
                        #endregion
                        break;
                    case "old":
                        #region oldCautionPanel
                        /// left row (bottom to top)
                        mapping[31] = DeduinoHelpers.CheckLight(LightBits2.SEC, BMSdata); //SEC
                        mapping[30] = DeduinoHelpers.CheckLight(LightBits.EngineFault, BMSdata); // ENGINE FAULT
                        mapping[29] = false; // INLET ICING
                        mapping[28] = DeduinoHelpers.CheckLight(LightBits3.Elec_Fault, BMSdata); // ELEC SYS
                        mapping[27] = DeduinoHelpers.CheckLight(LightBits3.cadc, BMSdata); // CADC
                        mapping[26] = DeduinoHelpers.CheckLight(LightBits3.Lef_Fault, BMSdata); // LE FLAPS
                        mapping[25] = false; // ADC
                        mapping[24] = DeduinoHelpers.CheckLight(LightBits.FltControlSys, BMSdata); // FLT CONT SYS
                        /// mid left row (bottom to top)
                        mapping[23] = false; //blank
                        mapping[22] = DeduinoHelpers.CheckLight(LightBits2.SEAT_ARM, BMSdata); // SEAT NOT ARMED
                        mapping[21] = DeduinoHelpers.CheckLight(LightBits2.FUEL_OIL_HOT, BMSdata); // FUEL OIL HOT
                        mapping[20] = DeduinoHelpers.CheckLight(LightBits2.BUC, BMSdata); // BUC
                        mapping[19] = false; // EEC
                        mapping[18] = DeduinoHelpers.CheckLight(LightBits.Overheat, BMSdata); // OVERHEAT
                        mapping[17] = DeduinoHelpers.CheckLight(LightBits2.AftFuelLow, BMSdata);// AFT FUEL LOW
                        mapping[16] = DeduinoHelpers.CheckLight(LightBits2.FwdFuelLow, BMSdata); // FWD FUEL LOW
                        /// mid right row (bottom to top)
                        mapping[15] = false; //blank
                        mapping[14] = DeduinoHelpers.CheckLight(LightBits.CONFIG, BMSdata); ; // STORES CONFIG
                        mapping[15] = DeduinoHelpers.CheckLight(LightBits.ECM, BMSdata); // ECM
                        mapping[13] = DeduinoHelpers.CheckLight(LightBits.IFF, BMSdata); // IFF
                        mapping[11] = DeduinoHelpers.CheckLight(LightBits.EQUIP_HOT, BMSdata); // EQUIP HOT
                        mapping[10] = DeduinoHelpers.CheckLight(LightBits.RadarAlt, BMSdata); // RADAR ALT
                        mapping[9] = false; // ATF NOT ENGAGED
                        mapping[8] = DeduinoHelpers.CheckLight(LightBits.Avionics, BMSdata); // AVIONICS
                        /// right row (bottom to top)
                        mapping[7] = false; //blank
                        mapping[6] = DeduinoHelpers.CheckLight(LightBits2.PROBEHEAT, BMSdata); // PROBE HEAT
                        mapping[5] = false; // NUCLEAR
                        mapping[4] = DeduinoHelpers.CheckLight(LightBits2.OXY_LOW, BMSdata); // OXY_LOW
                        mapping[3] = DeduinoHelpers.CheckLight(LightBits.CabinPress, BMSdata); // CABIN PRESS
                        mapping[2] = DeduinoHelpers.CheckLight(LightBits.NWSFail, BMSdata); // NWS FAILT
                        mapping[1] = DeduinoHelpers.CheckLight(LightBits.Hook, BMSdata); // HOOK
                        mapping[0] = DeduinoHelpers.CheckLight(LightBits2.ANTI_SKID, BMSdata); // ANTI SKID
                        #endregion
                        break;
                }
                if (appState.JshepCP)
                {
                    // Shep's CP is total non-sense as far as bit order goes.. put stuff in order for transmission
                    BitArray ShepCP = new BitArray(40, false);
                    ShepCP[0] = mapping[16];
                    ShepCP[1] = mapping[24];
                    ShepCP[2] = mapping[17];
                    ShepCP[3] = mapping[25];
                    ShepCP[4] = mapping[19];
                    ShepCP[5] = mapping[18];
                    ShepCP[6] = mapping[26];
                    ShepCP[7] = mapping[27];

                    ShepCP[8] = mapping[28];
                    ShepCP[9] = mapping[29];
                    ShepCP[10] = mapping[31];
                    ShepCP[11] = mapping[30];
                    ShepCP[12] = false;
                    ShepCP[13] = false;
                    ShepCP[14] = mapping[20];
                    ShepCP[15] = false;

                    ShepCP[16] = false;
                    ShepCP[17] = mapping[21];
                    ShepCP[18] = mapping[22];
                    ShepCP[19] = mapping[23];
                    ShepCP[20] = mapping[15];
                    ShepCP[21] = mapping[14];
                    ShepCP[22] = mapping[13];
                    ShepCP[23] = mapping[12];

                    ShepCP[24] = false;
                    ShepCP[25] = false;
                    ShepCP[26] = false;
                    ShepCP[27] = false;
                    ShepCP[28] = mapping[7];
                    ShepCP[29] = mapping[6];
                    ShepCP[30] = mapping[5];
                    ShepCP[31] = mapping[4];

                    ShepCP[32] = mapping[3];
                    ShepCP[33] = mapping[11];
                    ShepCP[34] = mapping[2];
                    ShepCP[35] = mapping[10];
                    ShepCP[36] = mapping[1];
                    ShepCP[37] = mapping[9];
                    ShepCP[38] = mapping[0];
                    ShepCP[39] = mapping[8];

                    ShepCP.CopyTo(result, 0);
                }
                else
                {
                    mapping.CopyTo(result, 0);
                }
                return result;
            }
            else //We are at lightcheck
            {
                for (short i = 0; i < result.Length; i++)
                {
                    result[i] = 255;
                }
                return result;
                //                return BitConverter.GetBytes(uint.MaxValue); // return all On.
            }

        }

        /// <summary>
        /// This function generates and returns a 2 byte array containing the glareshield lights
        /// </summary>
        /// <returns></returns>
        private byte[] MakeGlareShield()
        {
            BitArray mapping = new BitArray(16, false);
            byte[] result = new byte[mapping.Length];

            #region Generate_glareshield
            // Right side - top then bottom, from left to right
            mapping[0] = DeduinoHelpers.CheckLight(LightBits.ENG_FIRE, BMSdata); // Engine Fire
            mapping[1] = DeduinoHelpers.CheckLight(LightBits2.ENGINE, BMSdata); // Engine
            mapping[2] = DeduinoHelpers.CheckLight(LightBits.HYD, BMSdata); // HYD/OIL Press
            mapping[3] = DeduinoHelpers.CheckLight(LightBits.HYD, BMSdata); // HYD/OIL Press
            mapping[4] = DeduinoHelpers.CheckLight(LightBits.FLCS, BMSdata); // FLCS
            mapping[5] = DeduinoHelpers.CheckLight(LightBits3.DbuWarn, BMSdata); // DBU On
            mapping[6] = DeduinoHelpers.CheckLight(LightBits.T_L_CFG, BMSdata); //TO/LG Config
            mapping[7] = DeduinoHelpers.CheckLight(LightBits.T_L_CFG, BMSdata); //TO/LG Config
            mapping[8] = DeduinoHelpers.CheckLight(LightBits.CAN, BMSdata); // Canopy
            mapping[9] = DeduinoHelpers.CheckLight(LightBits.OXY_BROW, BMSdata); // OXY LOW (Brow)
            // Spacing
            mapping[10] = false; //spacer
            //left side - top then bottom, from left to right
            mapping[11] = DeduinoHelpers.CheckLight(LightBits.TF, BMSdata); // TF-FAIL
            mapping[12] = false; //blank
            mapping[13] = false;  //blank
            mapping[14] = false;  //blank
            // MC
            mapping[15] = DeduinoHelpers.CheckLight(LightBits.MasterCaution, BMSdata);  //Master Caution
            #endregion
            mapping.CopyTo(result, 0);
            return result;
        }

        /// <summary>
        /// This function takes two strings LINE and INV and mashes them into a string that conforms with the font on the Arduino Display
        /// This works for DED and PFL
        /// </summary>
        /// <param name="Disp"></param>
        /// <param name="Inv"></param>
        /// <returns></returns>
        private byte[] NormalizeLine(string Disp, string Inv)
        {
            char[] NormLine = new char[26]; // Create the result buffer
            for (short j = 0; j < Disp.Length; j++) // run the length of the Display string
            {
                if (Inv[j] == 2) // check if the corresponding position in the INV line is "lit" - indicated by char(2)
                { // if inverted
                    if (char.IsLetter(Disp[j])) // if char is letter (always uppercase)
                    {
                        NormLine[j] = char.ToLower((Disp[j])); // lowercase it - which is the inverted in the custom font
                    }
                    else if (Disp[j] == 1) // if it's the selection arrows
                    {
                        NormLine[j] = (char)192; // that is the selection arrow stuff
                    }
                    else if (Disp[j] == 2) // if it's a DED "*"
                    {
                        NormLine[j] = (char)170;
                    }
                    else if (Disp[j] == 3) // // if it's a DED "_"
                    {
                        NormLine[j] = (char)223;
                    }
                    else if (Disp[j] == '~') // Arrow down (PFD)
                    {
                        NormLine[j] = (char)252;
                    }
                    else if (Disp[j] == '^') // degree simbol (doesn't work with +128 from some reason so manualy do it
                    {
                        NormLine[j] = (char)222;
                    }
                    else // for everything else - just add 128 to the ASCII value (i.e numbers and so on)
                    {
                        NormLine[j] = (char)(Disp[j] + 128);
                    }
                }
                else // if it's non inverted
                {
                    if (Disp[j] == 1) // Selector double arrow
                    {
                        NormLine[j] = '@';
                    }
                    else if (Disp[j] == 2) // if it's a DED "*"
                    {
                        NormLine[j] = '*';
                    }
                    else if (Disp[j] == 3) // if it's a DED "_"
                    {
                        NormLine[j] = '_';
                    }
                    else if (Disp[j] == '~') // Arrow down (PFD)
                    {
                        NormLine[j] = '|';
                    }
                    else
                    {
                        NormLine[j] = Disp[j];
                    }
                }

            }
            if (appState.BMS432)
            {
                return Encoding.GetEncoding(1252).GetBytes(NormLine, 1, 24);
            }
            else
            {
                return Encoding.GetEncoding(1252).GetBytes(NormLine, 0, 24);
            }
        }

        private string cmdsMakeLine(short line)
        {
            string CMDSLine = "";
            if (DeduinoHelpers.CheckLight(LightBits2.Go, BMSdata) || DeduinoHelpers.CheckLight(LightBits2.NoGo, BMSdata)) // if either GO or NOGO flags are on system is on, run logic
            {
                if (line == 0)
                { // If top line needs to be handled
                    if (DeduinoHelpers.CheckLight(LightBits2.NoGo, BMSdata))
                    { // NoGo bit (5 Chars)
                        CMDSLine += "NO GO";
                    }
                    else
                    {
                        CMDSLine += "".PadLeft(5, ' ');
                    }
                    CMDSLine += "".PadLeft(2, ' '); //space between windows (2 chars)
                    if (DeduinoHelpers.CheckLight(LightBits2.Go, BMSdata))
                    { // Go bit (2 Chars)
                        CMDSLine += "GO";
                    }
                    else
                    {
                        CMDSLine += " ".PadLeft(2, ' ');
                    }

                    CMDSLine += " ".PadLeft(4, ' '); //space between windows (4 chars)
                    if (DeduinoHelpers.CheckLight(LightBits2.Rdy, BMSdata))
                    { // Go bit (12 Chars)
                        CMDSLine += "DISPENSE RDY";
                    }
                    else
                    {
                        CMDSLine += "".PadLeft(12, ' ');
                    }
                }
                else if (line == 1)
                { // If bottom line is to be handled
                    if (DeduinoHelpers.CheckLight(LightBits2.Degr, BMSdata))
                    { // degr  bit (9 Chars)
                        CMDSLine += "AUTO DEGR";
                    }
                    else
                    {
                        CMDSLine += " ".PadLeft(9, ' ');
                    }
                    CMDSLine += " ".PadLeft(3, ' ');//space between windows (5 chars)
                    // Chaff low
                    if (DeduinoHelpers.CheckLight(LightBits2.ChaffLo, BMSdata))
                    { //  (3 Chars)
                        CMDSLine += "LO";
                    }
                    else
                    {
                        CMDSLine += " ".PadLeft(2, ' ');
                    }
                    // CHaff logic
                    if (BMSdata.ChaffCount > 0) // if you have chaff
                    {
                        CMDSLine += BMSdata.ChaffCount.ToString().PadLeft(3, ' '); //print chaff count
                    }
                    else if (BMSdata.ChaffCount <= 0) // CM count of -1 = "out"
                    {
                        CMDSLine += "0".PadLeft(3, ' '); //print chaff count
                    }
                    else  // system is off or something
                    {
                        CMDSLine += " ".PadLeft(3, ' '); //send spaces
                    }

                    CMDSLine += "".PadLeft(1, ' '); //space between windows (1 chars)

                    if (DeduinoHelpers.CheckLight(LightBits2.FlareLo, BMSdata)) //Flare Low
                    { // (3 Chars)
                        CMDSLine += "LO";
                    }
                    else
                    {
                        CMDSLine += " ".PadLeft(2, ' ');
                    }

                    // Flare count logic
                    if (BMSdata.FlareCount > 0) // if you have cm
                    {
                        CMDSLine += BMSdata.FlareCount.ToString().PadLeft(3, ' '); //print chaff count
                    }
                    else if (BMSdata.FlareCount <= 0) // CM count of -1 = "out"
                    {
                        CMDSLine += "0".PadLeft(3, ' '); //print chaff count
                    }
                    else // system is off or something
                    {
                        CMDSLine += "0".PadLeft(3, ' '); //send spaces
                    }
                }
                else
                {
                    CMDSLine = "err";
                }
            }
            else
            { // system is off - send blank line
                CMDSLine = "".PadRight(24, ' ');
            }
            return CMDSLine;
        }

        /// <summary>
        /// This fuction returns speedbreaks indicator status.
        /// 0 - closed
        /// 1 - INOP
        /// 2 - Open
        /// </summary>
        /// <returns>
        /// 0 - closed
        /// 1 - INOP
        /// 2 - Open
        /// </returns>
        private byte[] MakeSpeedbreaks()
        {
            byte[] result = new byte[1];
            if (!DeduinoHelpers.CheckLight(PowerBits.BusPowerEmergency, BMSdata) && !appState.BMS432) //if emergency bus is down - speedbreaks indicator is INOP
            {
                result[0] = 1;
            }
            else if ((DeduinoHelpers.CheckLight(LightBits3.SpeedBrake, BMSdata)) && (BMSdata.speedBrake > 0.0)) // if speedbreaks are open
            {
                result[0] = 2;
            }
            else // if it's not INOP and not open - assume closed
            {
                result[0] = 0;
            }
            return result;
        }

        public void CloseApp()
        {
            //TODO: Maybe MainApp should be responsible for calling Application.Exit?
            Properties.Settings.Default.Save(); // Save settings before closing app
            if (serialComm.IsOpen)
            {
                try
                {
                    serialComm.CloseConnection(ref appState);
                    Application.Exit();
                }
                catch (InvalidCastException e)
                {
                    Application.Exit();
                }
            }
            else
            {
                Application.Exit();
            }
        }

        public bool OnStart(bool SetStart = false, bool startValue = true)
        {
            RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (!SetStart)
            {
                if (rkApp.GetValue(Application.ProductName) == null)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                if (startValue)
                {
                    rkApp.SetValue(Application.ProductName, Application.ExecutablePath.ToString());
                    return true;
                }
                else
                {
                    rkApp.DeleteValue(Application.ProductName, false);
                    return false;
                }
            }
        }
        public string checkVersion(string urlBase, ref ToolStripStatusLabel statusVersionInfo)
        //        async Task<string> checkVersion()
        {
            //HasCheckedVersion = true;
            string local = Application.ProductVersion + "\r\n";
            Version localver = new Version(local);

            try
            {
                WebClient wc = new WebClient();
                wc.Headers.Add("user-agent", "UI_Client_" + Application.ProductVersion);
                string remote = wc.DownloadString(@urlBase + "version/0.0.1/");
                Version remotever = new Version(remote);

                if (localver < remotever)
                {
                    if (MessageBox.Show("New version is available for download. Do you want to Dowload now?", "Update Available", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        string ProgramPath = Application.ExecutablePath;
                        string ProgramPathTmp = Application.ExecutablePath + ".tmp";
                        WebClient GetUpdate = new WebClient();
                        GetUpdate.Headers.Add("user-agent", "DEDuino_" + Application.ProductVersion);
                        GetUpdate.DownloadFile(@urlBase + "download/0.0.1/DEDuino.exe", ProgramPathTmp);
                        ProcessStartInfo updater = new ProcessStartInfo();
                        updater.Arguments = "/C ping 127.0.0.1 -n 2 > Nul & del \"" + ProgramPath + "\" & move \"" + ProgramPathTmp + "\" \"" + ProgramPath + "\" & \"" + ProgramPath + "\"";
                        updater.WindowStyle = ProcessWindowStyle.Hidden;
                        updater.CreateNoWindow = true;
                        updater.FileName = "cmd.exe";
                        Process.Start(updater);
                        Environment.Exit(0);
                    }
                    statusVersionInfo.Text = "Version: " + Application.ProductVersion;
                    //return;
                    return "Version: " + Application.ProductVersion;

                }
                else if (localver > remotever)
                {
                    statusVersionInfo.Text = Application.ProductVersion + "-BETA";
                    return Application.ProductVersion + "-BETA";
                }
                else
                {
                    //                StatusVersioninfo.Text = "Version: " + Application.ProductVersion;
                    return "Version: " + Application.ProductVersion;

                }
            }
            catch (WebException e)
            {
                //StatusVersioninfo.Text = "Version: " + Application.ProductVersion;
                //return;
                return "Version: " + Application.ProductVersion; ;
            }
        }


    }
}
