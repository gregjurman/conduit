using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using JurmanMetrics;

[assembly: CLSCompliant(true)]
namespace Conduit
{
    public partial class ProgressForm : Form
    {
        string host;
        string fileName;
        StreamWriter recvDataStream;
        FanucSocket fSocket;
        SaveFileDialog sfd;
        OpenFileDialog ofd;

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
            
            recvDataStream.Write(data);
        }

        void DataReceived(string data)
        {
            if (this.InvokeRequired)
                this.Invoke(new MethodInvoker(
                    delegate() { DataReceived(data); }
                    ));
            else
            {
                recvDataStream.Write(data);
            }
        }

        void fSocket_ChunkSent(object sender, double percentComplete)
        {
            ChunkSent(percentComplete);
        }

        void ChunkSent(double percent)
        {
            if (this.InvokeRequired)
                this.Invoke(new MethodInvoker(
                    delegate() { ChunkSent(percent); }
                    ));
            else
            {
                labelStatus.Text = fSocket.Status.ToString() + ".....  " + Math.Round(percent, 1) + "%";
                progressBar.Value = Convert.ToInt16(percent);
            }
        }

        void fSocket_ReceiveOperationCompleted(object sender, EventArgs e)
        {
            ReceiveComplete();
        }

        void ReceiveComplete()
        {
            if (this.InvokeRequired)
                this.Invoke(new MethodInvoker(
                    delegate() { ReceiveComplete(); }
                    ));
            else
            {
                progressBar.Hide();
                labelStatus.Hide();

                recvDataStream.Flush();
                recvDataStream.Close();

                fSocket.Close();

                MessageBox.Show("Receive complete!");
            }
        }

        void fSocket_SendOperationCompleted(object sender, EventArgs e)
        {
            SendComplete();
        }

        void SendComplete()
        {
            if (this.InvokeRequired)
                this.Invoke(new MethodInvoker(
                    delegate() { SendComplete(); }
                    ));
            else
            {
                progressBar.Hide();
                labelStatus.Hide();
                fSocket.Close();

                MessageBox.Show("Send complete!");
            }
        }

        void fSocket_StateChanged(object sender, FanucSocketState state)
        {
            StateChange(state.ToString());     
        }

        void StateChange(string state)
        {
            if (this.InvokeRequired)
                this.Invoke(new MethodInvoker(
                    delegate() { StateChange(state); }
                    ));
            else
            {
                labelStatus.Text = state;
            }
        }


        private void ProgressForm_Load(object sender, EventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();

            Regex argvarregex = new Regex(@"(?<=--)[\w]+(?==)");

            progressBar.Hide();
            labelStatus.Hide();
            host = "";
            fileName = "";

            args[0] = null;

            foreach (string arg in args)
            {
                if (!String.IsNullOrEmpty(arg))
                {
                    Match m = argvarregex.Match(arg);
                    if (m.Success)
                    {
                        if (m.Value.ToLower() == "host")
                        {
                            host = arg.Substring(7);
                        }
                    }
                    else //Probably a file
                    {
                        try
                        {
                            fileName = arg;

                            FileInfo fi = new FileInfo(fileName);

                            if (!fi.Exists)
                                throw new FileNotFoundException("File not found", fileName);

                            this.Text = "Conduit - " + fi.Name;
                        }
                        catch
                        {
                            throw;
                        }
                    }
                }

            }

            if (String.IsNullOrEmpty(host))
            {
                textController.Text = "";
                buttonReceive.Enabled = false;
                buttonSend.Enabled = false;
            }
            else
            {
                textController.Text = host;
                buttonReceive.Enabled = true;
                buttonSend.Enabled = true;
            }
        }

        /// <summary>
        /// Show the dialog box to open a file (if not already defined)
        /// connect to the FANUC and send the file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSend_Click(object sender, EventArgs e)
        {
            if ((String.IsNullOrEmpty(fileName)) || !(new FileInfo(fileName).Exists))
            {
                if (ofd == null) ofd = new OpenFileDialog();
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.DefaultExt = "txt";
                ofd.FileOk += new CancelEventHandler(ofdSend_FileOk);
                ofd.ShowDialog();
            }
            else
            {
                SendData();
            }
        }

        /// <summary>
        /// Callback for the OpenFileDialog
        /// </summary>
        /// <param name="sender">dialog object</param>
        /// <param name="e">Cancel params</param>
        void ofdSend_FileOk(object sender, CancelEventArgs e)
        {
            this.Activate();

            fileName = ((OpenFileDialog)sender).FileName;
            this.Text = "Conduit - " + ((OpenFileDialog)sender).SafeFileName;

            try
            {
                SendData();
            }
            catch
            {
                throw;
            }
        }
        /// <summary>
        /// Sends the file defined in fileName to the NC
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        void SendData()
        {
            StreamReader sr = new StreamReader(fileName);
            string dataIn = sr.ReadToEnd();

            try
            {
                progressBar.Show();
                labelStatus.Show();

                fSocket.Connect(textController.Text, 10001); //This is hard set, fix it (CONDUIT-7)
                fSocket.WriteOut(dataIn, FanucSocketOptions.FixNewline);
            }
            catch
            {
                sr.Dispose();
                sr = null;

                throw;
            }

            if (sr != null)
            {
                sr.Dispose();
                sr = null;
            }
        }

        /// <summary>
        /// Pulls in the file
        /// </summary>
        void ReceiveData()
        {
            try
            {
                progressBar.Show();
                labelStatus.Show();
                fSocket.Connect(textController.Text, 10001); //This is hard set, fix it
                fSocket.ReadIn(FanucSocketOptions.FixNewline);
            }
            catch
            {
                throw;
            }
        }

        private void buttonReceive_Click(object sender, EventArgs e)
        {
            if ((String.IsNullOrEmpty(fileName)) || !(new FileInfo(fileName).Exists))
            {
                if (sfd == null) sfd = new SaveFileDialog();
                sfd.CheckFileExists = false;
                sfd.DefaultExt = "txt";
                sfd.FileOk += new CancelEventHandler(sfd_FileOk);
                sfd.ShowDialog();
            }
            else
            {
                try
                {
                    recvDataStream = new StreamWriter(fileName, false, Encoding.ASCII, 1024);
                    ReceiveData();
                }
                catch
                {
                    throw;
                }
            }
        }

        void sfd_FileOk(object sender, CancelEventArgs e)
        {
            this.Activate();

            fileName = ((SaveFileDialog)sender).FileName;
            this.Text = "Conduit";

            try
            {
                recvDataStream = new StreamWriter(fileName, false, Encoding.ASCII, 1024);
                ReceiveData();
            }
            catch
            {
                throw;
            }
        }

        private void textController_TextChanged(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(textController.Text))
            {
                buttonReceive.Enabled = false;
                buttonSend.Enabled = false;
            }
            else
            {
                buttonReceive.Enabled = true;
                buttonSend.Enabled = true;
            }
        }
    }
}
