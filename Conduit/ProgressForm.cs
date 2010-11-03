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
        StreamWriter recvDataStream;
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

                MessageBox.Show("Receive complete!");
            }
        }

        void fSocket_SendOperationCompleted(object sender, EventArgs e)
        {
            progressBar.Hide();
            labelStatus.Hide();

            MessageBox.Show("Send complete!");
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

            progressBar.Hide();
            labelStatus.Hide();
            host = "";
            fileName = "";

            if (args.Length == 2)
            {
                host = args[1];
                textController.Text = host;
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

        /// <summary>
        /// Show the dialog box to open a file (if not already defined)
        /// connect to the FANUC and send the file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSend_Click(object sender, EventArgs e)
        {
            if ((fileName=="") || !(new FileInfo(fileName).Exists))
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.DefaultExt = "txt";
                ofd.FileOk += new CancelEventHandler(ofdSend_FileOk);
                ofd.ShowDialog();
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
            catch (Exception exp)
            {
                
            }
            finally
            {
                fSocket.Close();
            }
        }

        /// <summary>
        /// Sends the file defined in fileName to the NC
        /// </summary>
        void SendData()
        {
            try
            {
                progressBar.Show();
                labelStatus.Show();
                StreamReader sr = new StreamReader(fileName);
                string dataIn = sr.ReadToEnd();

                sr.Close();

                fSocket.Connect(textController.Text, 10001); //This is hard set, fix it
                fSocket.WriteOut(dataIn, FanucSocketOptions.FixNewline);
            }
            catch (Exception e)
            {
                throw e;
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
            catch (Exception e)
            {
                throw e;
            }
        }

        private void buttonReceive_Click(object sender, EventArgs e)
        {
            if ((fileName == "") || !(new FileInfo(fileName).Exists))
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.CheckFileExists = false;
                sfd.DefaultExt = "txt";
                sfd.FileOk += new CancelEventHandler(sfd_FileOk);
                sfd.ShowDialog();
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
            catch (Exception exp)
            {

            }
            finally
            {
                fSocket.Close();
            }
        }
    }
}
