using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace JurmanMetrics
{
    public enum FanucSocketState { Idle = 0, Waiting, Sending, Receiving, NotConnected = 1000, Unknown = 9999 };
    public enum FanucChunkState { UnProcessed = 0, ReadyForProcessing, Processed, Ignore, Error = 1000 };
    public enum FanucSocketOptions { None = 0, FixNewline = 1, IgnoreControlCodes = 2, RawMode = 4, NoEndBlock = 8, NoStartBlock = 16 };

    class FanucSocket
    {
        // CONSTANTS
        readonly int BufferSize;    // The size of the buffers
        readonly int SendDelay;     // The delay used between each sent chunk

        // CORE COMMUNICATION VARIABLES
        TcpClient tc;               // The TCPClient used to create the socket connection
        NetworkStream ns;           // The NetworkStream used to send and receive data

        // THREADING
        SemaphoreSlim nsLock;       // Semaphore for controlling access to the NetworkStream
        Task operation;
        CancellationTokenSource cancelSource;
        CancellationToken opCancelToken;

        // STATUS OBJECT
        FanucSocketState state;

        // DELEGATES
        public delegate void DataReceivedEventHandler(object sender, string data);
        public delegate void DataUpdateEventHander(object sender, double percentComplete);
        public delegate void ErrorEventHandler(object sender, Exception e);
        public delegate void StateChangeEventHander(object sender, FanucSocketState state);

        // EVENTS
        public event EventHandler ReceiveOperationCompleted;
        public event EventHandler SendOperationCompleted;
        public event DataReceivedEventHandler ChunkReceived;
        public event DataReceivedEventHandler BadChunkReceived;
        public event DataUpdateEventHander ChunkSent;
        public event ErrorEventHandler ExceptionOccurred;
        public event StateChangeEventHander StateChanged;

        /// <summary>
        /// Constructor
        /// </summary>
        public FanucSocket(int bufferSize, int sendDelay)
        {
            BufferSize = bufferSize;
            SendDelay = sendDelay;

            nsLock = new SemaphoreSlim(1, 1);

            ChangeState(FanucSocketState.NotConnected);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public FanucSocket()
        {
            BufferSize = 960;       // 960 bytes
            SendDelay = 1000;        // 1S

            nsLock = new SemaphoreSlim(1, 1);

            ChangeState(FanucSocketState.NotConnected);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hostName">The hostname of the device.</param>
        /// <param name="port">The port number to connect to.</param>
        public void Connect(string hostName, int port)
        {
            try
            {
                if (tc == null) tc = new TcpClient();
                tc.Connect(hostName, port);

                ns = tc.GetStream();

                ns.ReadTimeout = 5000;
                ns.WriteTimeout = 5000;
                ChangeState(FanucSocketState.Idle);
            }

            catch (SocketException se)
            {
                ChangeState(FanucSocketState.NotConnected);
            }
        }

        /// <summary>
        /// Closes the socket.
        /// </summary>
        public void Close()
        {
            StopOperation();

            if (ns != null)
            {
                ns.Close();
                ns.Dispose();
                ns = null;
            }
            if (tc != null)
            {
                tc.Close();
            }

            ChangeState(FanucSocketState.NotConnected);
        }

        /// <summary>
        /// Check all threads and tasks and confirm there is nothing preventing a task from running
        /// </summary>
        /// <returns>If it is safe to proceed with the requested operation.</returns>
        private bool SafeToProceed()
        {
            bool status = false;

            status = (nsLock.CurrentCount == 1) && (operation == null);

            return status;
        }

        public FanucSocketState Status
        {
            get 
            { return state; }
        }

        public void StopOperation()
        {
            if (cancelSource != null) 
            {
                cancelSource.Cancel();

                ChangeState(FanucSocketState.Idle);
            }
        }

        private void ChangeState(FanucSocketState newState)
        {
            state = newState;
            if (StateChanged != null) StateChanged.Invoke(this, state);
        }

        /// <summary>
        /// Begin a task reading data from the Fanuc Device
        /// </summary>
        public void ReadIn(FanucSocketOptions options)
        {
            if (SafeToProceed())
            {
                ns.Flush(); // Removes extra data that is stuck in the socket
                
                cancelSource = new CancellationTokenSource();
                opCancelToken = cancelSource.Token;

                // Main read task
                operation = new Task(
                    cancellationToken: cancelSource.Token,
                    creationOptions: TaskCreationOptions.LongRunning,
                    action: () =>
                        {
                            this.ReadIn_Main(options);
                        }
                    );

                // If data receiption completes fully, fire event and dispose
                Task continuation = operation.ContinueWith(
                continuationOptions: TaskContinuationOptions.ExecuteSynchronously,
                continuationAction: (t) =>
                    {
                        if (t.IsCompleted)
                        {
                            if (ReceiveOperationCompleted != null) ReceiveOperationCompleted.Invoke(this, null);
                        }
                        else if (t.IsFaulted)
                        {
                            if (ExceptionOccurred != null) ExceptionOccurred.Invoke(this, t.Exception);
                        }

                        cancelSource.Dispose();
                        cancelSource = null;

                        operation.Dispose();
                        operation = null;

                        ChangeState(FanucSocketState.Idle);
                    }
                );

                operation.Start();
            }

            else throw new Exception("Operation in progress!");
        }

        /// <summary>
        /// Begin a task reading data from the Fanuc Device
        /// </summary>
        public void WriteOut(string data, FanucSocketOptions options)
        {
            if (SafeToProceed())
            {
                ns.Flush(); // Removes extra data that is stuck in the socket

                cancelSource = new CancellationTokenSource();
                opCancelToken = cancelSource.Token;

                // Main read task
                operation = new Task(
                    cancellationToken: cancelSource.Token,
                    creationOptions: TaskCreationOptions.LongRunning,
                    action: () =>
                    {
                        this.WriteOut_Main(data, options);
                    }
                    );

                // If sending completes fully, fire event and dispose
                Task continuation = operation.ContinueWith(
                continuationOptions: TaskContinuationOptions.ExecuteSynchronously,
                continuationAction: (t) =>
                {
                    if (t.IsCompleted)
                    {
                        if (SendOperationCompleted != null) SendOperationCompleted.Invoke(this, null);
                    }
                    else if (t.IsFaulted)
                    {
                        if (ExceptionOccurred != null) ExceptionOccurred.Invoke(this, t.Exception);
                    }

                    cancelSource.Dispose();
                    cancelSource = null;

                    operation.Dispose();
                    operation = null;

                    ChangeState(FanucSocketState.Idle);
                }
                );

                operation.Start();
            }

            else throw new Exception("Operation in progress!");
        }

        /// <summary>
        /// Locks access to the socket and does a read operation. If data has arrived, it is checked for Fanuc
        ///  control codes and saved to memory.
        /// </summary>
        /// <returns>A FanucChunk with the complete data and status</returns>
        private void ReadIn_Main(FanucSocketOptions options)
        {
            FanucChunk chunk;

            opCancelToken.ThrowIfCancellationRequested();

            chunk = new FanucChunk(0);

            ChangeState(FanucSocketState.Receiving);

            while (!opCancelToken.IsCancellationRequested)
            {
                if (ns.DataAvailable)
                {
                    ReadIn_GetData(ref chunk);

                    if (chunk.State == FanucChunkState.ReadyForProcessing)
                    {
                        ReadIn_ParseData(ref chunk, options);
                    }

                    if (chunk.State == FanucChunkState.Processed)
                    {
                        if (ChunkReceived != null) ChunkReceived.Invoke(this, chunk.Data);
                    }
                    else if (chunk.State == FanucChunkState.Error)
                    {
                        if (BadChunkReceived != null) BadChunkReceived.Invoke(this, chunk.Data);
                    }

                    if (!options.HasFlag(FanucSocketOptions.IgnoreControlCodes))
                        if ((int)chunk.PersistentExtra == 0x14)
                            return;

                    chunk = new FanucChunk(chunk);
                }
                else
                {
                    Thread.Sleep(500);
                }

                opCancelToken.ThrowIfCancellationRequested();
            }

            opCancelToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Gets data from socket and puts it in the chunk
        /// </summary>
        /// <param name="chunk">The chunk to work with</param>
        private void ReadIn_GetData(ref FanucChunk chunk)
        {
            byte[] readBuffer = new byte[BufferSize];

            nsLock.Wait(opCancelToken);
            int iRX = ns.Read(readBuffer, 0, BufferSize);
            nsLock.Release();

            chunk.Data = Encoding.ASCII.GetString(readBuffer, 0, iRX);

            chunk.State = FanucChunkState.ReadyForProcessing;
        }

        /// <summary>
        /// Parses the data and keeps a state
        /// </summary>
        /// <param name="chunk">The chunk to process</param>
        /// <param name="options">Processing options</param>
        private void ReadIn_ParseData(ref FanucChunk chunk, FanucSocketOptions options)
        {
            if (!options.HasFlag(FanucSocketOptions.RawMode))
            {
                if (!options.HasFlag(FanucSocketOptions.IgnoreControlCodes))
                {
                    ReadIn_ClipBlock(ref chunk);
                }

                if (options.HasFlag(FanucSocketOptions.FixNewline) && chunk.State != FanucChunkState.Error)
                {
                    ReadIn_ToCRLF(ref chunk);
                }
            }
            if (chunk.State == FanucChunkState.ReadyForProcessing)
            {
                chunk.State = FanucChunkState.Processed;
            }
        }


        /// <summary>
        /// Converts LF to CR+LF
        /// </summary>
        /// <param name="chunk">The chunk to process</param>
        private void ReadIn_ToCRLF(ref FanucChunk chunk)
        {
            chunk.Data = Regex.Replace(chunk.Data, "(?<!\r)\n\n", "\r\n");
            chunk.Data = Regex.Replace(chunk.Data, "(?<!\r)\n", "\r\n");
        }

        /// <summary>
        /// Crops the data in the DC2-DC4 bounds
        /// </summary>
        /// <param name="chunk">The chunk to process</param>
        private void ReadIn_ClipBlock(ref FanucChunk chunk)
        {
            Regex datacodes = new Regex(@"[\x12\x14]");
            MatchCollection matches = datacodes.Matches(chunk.Data);

            if (matches.Count == 0) // No control codes found
            {
                if ((int)chunk.PersistentExtra != 0x12) // The last persistant code was not a DC2
                {
                    chunk.State = FanucChunkState.Ignore; // Ignore this chunk, it isn't in a block
                }
            }
            else
            {
                int cutstart = 0;
                int cutlength = chunk.Data.Length;

                foreach (Match m in matches)
                {
                    int code = (int)chunk.Data[(m.Index)];
                    if (code == 0x12)  //DC2 - Enter Block
                    {
                        if ((int)chunk.PersistentExtra == 0x00) // We have no prev control code, this is correct
                        {
                            cutstart = m.Index + 1;
                            cutlength -= cutstart;
                            chunk.PersistentExtra = 0x12;
                        }
                        else // Something is broken with this chunk, error this chunk
                        {
                            chunk.State = FanucChunkState.Error;
                        }
                    }
                    else if (code == 0x14)
                    {
                        if ((int)chunk.PersistentExtra == 0x12) //We are in a block
                        {
                            cutlength = m.Index - cutstart;
                            chunk.PersistentExtra = 0x14;
                        }
                        else // Either we missed something or something is broken, error this chunk
                        {
                            chunk.State = FanucChunkState.Error;
                        }
                    }
                    else
                    {
                        throw new Exception("ReadIn_ClipBlock: An unknown code was parsed. It was ASCII code "
                            + Convert.ToString((int)chunk.Data[(m.Index)]) + ".");
                    }
                }

                // We have found our endpoints, clip the data as long as its clean
                if (chunk.State != FanucChunkState.Error) chunk.Data = chunk.Data.Substring(cutstart, cutlength);
            }
        }

        private void WriteOut_Main(string data, FanucSocketOptions options)
        {
            FanucChunk[] chunks;

            opCancelToken.ThrowIfCancellationRequested();

            WriteOut_Prepare(out chunks, ref data, options);

            bool OkayToSend = false;
            int lastCode = 0;
            int cId = 0;

           ChangeState(FanucSocketState.Waiting);

            while(cId < chunks.Length)
            {
                lastCode = WriteOut_GetCode();

                if ((lastCode == 0x11) && !OkayToSend)
                {
                    OkayToSend = true; //Got DC1, Cleared to send
                    ChangeState(FanucSocketState.Sending);
                }
                else if ((lastCode == 0x13) && OkayToSend)
                {
                    OkayToSend = false; //Got DC3, Pause until its cleared
                    ChangeState(FanucSocketState.Waiting);
                }

                if (OkayToSend)
                {
                    WriteOut_SendChunk(chunks[cId]);
                    cId++;
                    
                    if (ChunkSent != null) ChunkSent.Invoke(this, Math.Round((cId / (double)chunks.Length) * 100, 1));
                }

                opCancelToken.ThrowIfCancellationRequested();

                Thread.Sleep(SendDelay);

                opCancelToken.ThrowIfCancellationRequested();
            }

            opCancelToken.ThrowIfCancellationRequested();
        }

        private int WriteOut_GetCode()
        {
            int lastCode = 0;

            if (ns.DataAvailable)
            {
                nsLock.Wait();
                int RX = ns.ReadByte();
                nsLock.Release();

                if ((RX < 0x11) && (RX > 0x14))
                    RX = 0;
                else
                    lastCode = RX;
            }

            return lastCode;
        }

        private void WriteOut_SendChunk(FanucChunk chunk)
        {
            byte[] temp = ASCIIEncoding.ASCII.GetBytes(chunk.Data);

            nsLock.Wait();
            ns.Write(temp, 0, temp.Length);
            nsLock.Release();
        }

        private void WriteOut_Prepare(out FanucChunk[] chunks, ref string data, FanucSocketOptions options)
        {
            if (!options.HasFlag(FanucSocketOptions.RawMode))
            {
                WriteOut_ToLF(ref data); // Correct newlines
                WriteOut_Sanitize(ref data, options); // Sanitize data
            }

            WriteOut_ChunkData(out chunks, data);
        }


        /// <summary>
        /// Santizes data before sending it out. Needs some work
        /// </summary>
        /// <param name="data">The data string</param>
        private void WriteOut_Sanitize(ref string data, FanucSocketOptions options)
        {
            //Remove control codes left over in old files
            data = Regex.Replace(data, @"[\x11-\x14]", "");

            if (!options.HasFlag(FanucSocketOptions.NoStartBlock))
            {
                // Trim junk to the first program block
                Match m = Regex.Match(data, @"[:oO][\d]{4}");

                if (m != null)
                {
                    data = data.Substring(m.Index);
                    data = "%\n" + data;
                }
                else
                {
                    throw new Exception("No sub-programs found!");
                }
            }
            if (!options.HasFlag(FanucSocketOptions.NoEndBlock))
            {
                Match m = Regex.Match(data, "%", RegexOptions.RightToLeft);

                //If it found a % (it better i JUST put one there)
                if (m.Index != 0)
                {
                    data = data.Substring(0, m.Index + 1);
                }
                else
                {
                    //Fix this, its a 2AM hack.
                    data += "%"; // Append one to the end
                }
            }
        }

        private void WriteOut_ToLF(ref string data)
        {
            data = Regex.Replace(data, "\r\n", "\n");
        }

        private void WriteOut_ChunkData(out FanucChunk[] chunks, string data)
        {
            List<FanucChunk> chunkTemp = new List<FanucChunk>();

            while (data.Length > 0)
            {
                FanucChunk fc = new FanucChunk(chunkTemp.Count);
                int length = (BufferSize <= data.Length) ? BufferSize : data.Length;

                fc.Data = data.Substring(0, length);
                fc.State = FanucChunkState.ReadyForProcessing;

                chunkTemp.Add(fc);

                data = data.Remove(0, length);
            }

            chunks = chunkTemp.ToArray();

            chunkTemp.Clear();
        }
    }
}
