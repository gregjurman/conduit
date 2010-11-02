using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using JurmanMetrics;


namespace Conduit
{
    public partial class ProgressForm : Form
    {
        string host;
        string fileName;
        Stream fileDataStream;
        FanucSocket fSocket;

        public ProgressForm()
        {
            InitializeComponent();

            fSocket = new FanucSocket(100, 200);
            fSocket.StateChanged += new FanucSocket.StateChangeEventHander(fSocket_StateChanged);
            fSocket.SendOperationCompleted += new EventHandler(fSocket_SendOperationCompleted);
            fSocket.ReceiveOperationCompleted += new EventHandler(fSocket_ReceiveOperationCompleted);
            fSocket.ChunkSent += new FanucSocket.DataUpdateEventHander(fSocket_ChunkSent);
            fSocket.ChunkReceived += new FanucSocket.DataReceivedEventHandler(fSocket_ChunkReceived);
        }

        void fSocket_ChunkReceived(object sender, string data)
        {
            throw new NotImplementedException();
        }

        void fSocket_ChunkSent(object sender, double percentComplete)
        {
            throw new NotImplementedException();
        }

        void fSocket_ReceiveOperationCompleted(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        void fSocket_SendOperationCompleted(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        void fSocket_StateChanged(object sender, FanucSocketState state)
        {
            throw new NotImplementedException();
        }

        private void ProgressForm_Load(object sender, EventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();

            host = "";
            fileName = "";

            if (args.Length == 2)
            {
                host = args[1];
            }
            if (args.Length == 3)
            {
                host = args[1];
                fileName = args[2];
                try
                {
                    FileInfo fi = new FileInfo(fileName);

                    if (!fi.Exists)
                        throw new FileNotFoundException("File not found", fileName);
                }
                catch (Exception excp)
                {
                    MessageBox.Show(
                        text: excp.Message,
                        caption: "Conduit - Unable to Open File",
                        buttons: MessageBoxButtons.OK,
                        icon: MessageBoxIcon.Error,
                        defaultButton: MessageBoxDefaultButton.Button1
                    );
                }
            }

            if (host == "")
            {

            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// Show the dialog box to open a file (if not already defined)
        /// connect to the FANUC and send the file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSend_Click(object sender, EventArgs e)
        {
            if (!(new FileInfo(fileName).Exists))
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.DefaultExt = "txt";
                ofd.FileOk += new CancelEventHandler(ofdSend_FileOk);
                ofd.ShowDialog();
            }
        }

        void ofdSend_FileOk(object sender, CancelEventArgs e)
        {
            this.Activate();

            fileName = ((OpenFileDialog)sender).FileName;
            this.Text = "Conduit - " + ((OpenFileDialog)sender).SafeFileName;

            try
            {
                SendData();
            }
            catch (Exception exp)
            {
                
            }
            finally
            {
                fSocket.Close();
            }
        }

        void SendData()
        {
            try
            {
                StreamReader sr = new StreamReader(fileName);
                string dataIn = sr.ReadToEnd();

                fSocket.Connect(host, 10001); //This is hard set, fix it
                fSocket.WriteOut(dataIn, FanucSocketOptions.FixNewline);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
