﻿//#define DEBUG
#undef DEBUG

/*
DEDuino
 * Software connector to allow Transmission of data to supported Arduino code
 * Written by Uri Ben-Avrahm - 2014
 * http://pits.108vfs.org
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Concurrent;
using System.Data;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections;
using System.Net;
using Microsoft.Win32;
using F4SharedMem;


namespace DEDuino
{

    public partial class MainWindow : Form
    {
        #region Declarations
        public const string URLBase = @"http://files.108vfs.org/deduino/"; // URL for Updater
        public F4SharedMem.Reader realBMSreader = new F4SharedMem.Reader();
        public IBMSReader BMSreader;
        public FlightData BMSdata = new FlightData();        
        public ISerialComm dedDevice = new SerialComm();
        public char SerialBuffer;
        private AppState appState;
        private DedFunctions dedFunctions;        
        #endregion

        public MainWindow()
        {
            BMSreader = new BMSReaderWrapper(realBMSreader);  // we should now be able to stub in a fake BMS reader for testing purposes.
            appState = new AppState(Properties.Settings.Default.CautionPanel);
            appState.CautionPanelVer = Properties.Settings.Default.CautionPanel;
            appState.BMS432 = Properties.Settings.Default.BMS432;
            appState.JshepCP = Properties.Settings.Default.JshepCP;
            dedFunctions = new DedFunctions(dedDevice, ref appState, ref BMSreader, ref BMSdata);

            InitializeComponent();
            Thread updater = new System.Threading.Thread(delegate()
           {
               StatusVersioninfo.Text = dedFunctions.checkVersion(URLBase, ref StatusVersioninfo); // Check if a new version is available
           });
            updater.Start();
            ResetTheBoard();
            dedFunctions.updateAvailablePorts(ref comboBoxComSelect); //Scan for Available serial port on the computer           
        }

        private void buttonStart_Click(object sender, EventArgs e)
        /*
         * Logic for start button press.
         */
        {
            Properties.Settings.Default.Save(); //Save settings first..
            if (dedDevice.IsOpen) //if serial connection is open - close it.
            {
                #region DisconnectSerial
                if (dedFunctions.SerialInit(ref appState, checkBox_isUno.Checked, comboBoxComSelect.SelectedValue.ToString(), ref toolStripStatusComConnection)) //Issue SerialPort disconnect via "serialinit" function 
                { // if disconnection was successful - reset the button to starting position
                    toolStripStatusComConnection.Text = "Ready";
                    toolStripStatusComConnection.BackColor = SystemColors.Control;
                    SysTeayIcon.Icon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("DEDuino.AE_systray2_gd.ico"));
                    buttonStart.Text = "Start";
                    SystrayMenuItemStart.Text = "Start";
                    comboBoxComSelect.Enabled = true;
                    checkBox_isUno.Enabled = true;
                    tabPageAdv.Enabled = true;

                }
                else // if hangup failed
                {
                    toolStripStatusComConnection.Text = "Error";
                    toolStripStatusComConnection.BackColor = Color.Red;
                }
                #endregion
            }
            else if (comboBoxComSelect.SelectedValue != null) //if no serial connection is open and the is a COM device selected 
            {
                if (!dedDevice.IsOpen && SerialPort.GetPortNames().Contains(comboBoxComSelect.SelectedValue))
                {
                    #region ConnectSerial
                    if (dedFunctions.SerialInit(ref appState, checkBox_isUno.Checked, comboBoxComSelect.SelectedValue.ToString(), ref toolStripStatusComConnection)) //issue Connect command
                    { //if connection succeded - change button to allow disconnect
                        toolStripStatusComConnection.Text = "Connected";
                        toolStripStatusComConnection.BackColor = Color.Green;
                        SysTeayIcon.Icon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("DEDuino.AE_systray2_gu.ico"));
                        buttonStart.Text = "Stop";
                        SystrayMenuItemStart.Text = "Stop";
                        comboBoxComSelect.Enabled = false;
                        checkBox_isUno.Enabled = false;
                        tabPageAdv.Enabled = false;
                        ControlSize();
                    }
                    else
                    { //if connection failed
                        toolStripStatusComConnection.Text = "Error";
                        toolStripStatusComConnection.BackColor = Color.Red;
                    }
                    #endregion
                }
                else
                {
                    toolStripStatusComConnection.Text = "Selected COM port not valid";
                    toolStripStatusComConnection.BackColor = Color.Yellow;
                }
            }
            else if (comboBoxComSelect.SelectedValue == null) // if no serial connection is selected
            {
                toolStripStatusComConnection.Text = "No Arduino present";
                toolStripStatusComConnection.BackColor = Color.Yellow;
            }
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dedFunctions.CloseApp();
        }

        private void refreshCOMListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dedDevice.CheckPorts();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = "DEDuino Project\r\nVersion: " + Application.ProductVersion + "\r\nFalcon DED extractor for Arduino.\r\n© Uri Ben-Avraham 2015";
            MessageBox.Show(message);
        }

        private void comboBoxComSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            DEDuino.Properties.Settings.Default.COMport = comboBoxComSelect.Text;

        }

        private void checkBox_isUno_CheckedChanged(object sender, EventArgs e)
        {
            DEDuino.Properties.Settings.Default.isUno = checkBox_isUno.Checked;
            Properties.Settings.Default.Save(); // Save settings
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ControlSize();
            //if (this.WindowState == FormWindowState.Minimized)
            //{
            //    this.ShowInTaskbar = true;
            //    this.FormBorderStyle = FormBorderStyle.FixedSingle;
            //    this.Visible = true;
            //    this.WindowState = FormWindowState.Normal;
            //    this.SystrayMenuItemShow.Text = "&Hide";
            //}
            //else
            //{
            //    this.Visible = false;
            //    this.ShowInTaskbar = false;
            //    this.WindowState = FormWindowState.Minimized;
            //    this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            //    this.SystrayMenuItemShow.Text = "&Show";
            //}
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dedFunctions.CloseApp();
        }

        private void MainWindow_Resize(object sender, EventArgs e)
        {
            //ControlSize();
            switch (this.WindowState)
            {
                case FormWindowState.Maximized:
                    this.FormBorderStyle = FormBorderStyle.FixedSingle;
                    this.WindowState = FormWindowState.Normal;
                    this.SystrayMenuItemShow.Text = "Hide";
                    this.ShowInTaskbar = true;
                    this.Visible = true; break;
                case FormWindowState.Minimized:
                    this.Visible = false;
                    this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                    this.ShowInTaskbar = false;
                    this.WindowState = FormWindowState.Minimized;
                    this.SystrayMenuItemShow.Text = "Show";
                    break;
                case FormWindowState.Normal:
                    this.FormBorderStyle = FormBorderStyle.FixedSingle;
                    this.WindowState = FormWindowState.Normal;
                    this.SystrayMenuItemShow.Text = "Hide";
                    this.ShowInTaskbar = true;
                    this.Visible = true;
                    break;
                default:
                    break;
            }

        }

        private void comboBoxComSelect_DropDown(object sender, EventArgs e)
        {
            dedDevice.CheckPorts();
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save(); // Save settings on close

        }

        private void radioButtonCaution_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonCautionNew.Checked)
            {
                appState.CautionPanelVer = "new";
            }
            else if (radioButtonCautionOld.Checked)
            {
                appState.CautionPanelVer = "old";
            }
            Properties.Settings.Default.CautionPanel = appState.CautionPanelVer;
            Properties.Settings.Default.Save(); // Save settings
        }

        private void Checkbox_BMS432_CheckedChanged(object sender, EventArgs e)
        {
            appState.BMS432 = Checkbox_BMS432.Checked;
            Properties.Settings.Default.BMS432 = Checkbox_BMS432.Checked;
            Properties.Settings.Default.Save(); // Save settings

        }

        private void checkBox_onStartup_CheckedChanged(object sender, EventArgs e)
        {
            dedFunctions.OnStart(SetStart: true, startValue: checkBox_onStartup.Checked);
            Properties.Settings.Default.onStartup = checkBox_onStartup.Checked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_JshepCP_CheckedChanged(object sender, EventArgs e)
        {
            appState.JshepCP = checkBox_JshepCP.Checked;
            Properties.Settings.Default.JshepCP = appState.JshepCP;
            Properties.Settings.Default.Save();
        }
        private void SystrayMenuItemShow_Click(object sender, EventArgs e)
        {
            ControlSize();
        }

        private void ControlSize()
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                //this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.WindowState = FormWindowState.Normal;
                this.SystrayMenuItemShow.Text = "Hide";
                this.ShowInTaskbar = true;
                this.Visible = true;
            }
            else
            {
                this.Visible = false;
                this.ShowInTaskbar = false;
                this.WindowState = FormWindowState.Minimized;
                //this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                this.SystrayMenuItemShow.Text = "Show";
            }
        }
        private void ResetTheBoard()
        {
            checkBox_isUno.Checked = DEDuino.Properties.Settings.Default.isUno;
            comboBoxComSelect.Text = DEDuino.Properties.Settings.Default.COMport;
            Checkbox_BMS432.Checked = DEDuino.Properties.Settings.Default.BMS432;
            checkBox_onStartup.Checked = dedFunctions.OnStart(SetStart: false);
            checkBox_JshepCP.Checked = DEDuino.Properties.Settings.Default.JshepCP;
            if (checkBox_onStartup.Checked)
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }
            if (Properties.Settings.Default.CautionPanel == "new")
            {
                radioButtonCautionNew.Checked = true;
            }
            else
            {
                radioButtonCautionOld.Checked = true;
            }
        }
    }
}
