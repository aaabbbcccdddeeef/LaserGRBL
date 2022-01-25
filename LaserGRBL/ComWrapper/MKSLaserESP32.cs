using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using System.Threading;
using System.Net;
using System.Windows.Forms;

namespace LaserGRBL.ComWrapper

{
    class MKSLaserESP32 : IComWrapper
    {
        private string mAddress;
        private WebSocket cln;
        private Queue<string> buffer = new Queue<string>();
        byte[] buffers = new byte[2048];
        Socket socket;
        Thread thread;
        bool CanRead = true;

        public bool IsOpen
        { get { return socket != null && socket.Connected; } }

        public void setCanRead(bool isCan)
        {
            CanRead = isCan;
        }

        public void Close(bool auto)
        {
            if (socket != null)
            {
                try
                {
                    ComLogger.Log("mks--com", string.Format("Close {0} [{1}]", mAddress, auto ? "CORE" : "USER"));
                    Logger.LogMessage("mks--CloseCom", "Close {0} [{1}]", mAddress, auto ? "CORE" : "USER");
                    /*socket.Disconnect(true);*/
                    socket.Close();
                  /*  socket.Dispose();
                    socket = null;*/
                    if (thread != null)
                    {
                        thread.Abort();
                    }
                }
                catch { }
            }
        }

        public void Configure(params object[] param)
        {
            mAddress = (string)param[0];
        }

        public bool HasData()
        { return IsOpen && buffer.Count > 0; }

        public void Open()
        {
            if (socket != null)
                Close(true);
            if (string.IsNullOrEmpty(mAddress))
                throw new MissingFieldException("Missing Address");

            try
            {
                //实例化socket
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //连接服务器
                socket.Connect(new IPEndPoint(IPAddress.Parse(mAddress), 8080));

                thread = new Thread(StartReceive);
                thread.IsBackground = true;
                thread.Start(socket);
                CanRead = true;
            }
            catch (Exception ex)
            {
                SetMessage("Open服务器异常:" + ex.Message);
                if (thread != null)
                {
                    thread.Abort();
                }
            }
        }
        private void StartReceive(object obj)
        {
            string str = "";
            while (true)
            {
                Socket receiveSocket = obj as Socket;
                /*receiveSocket.Send(System.Text.Encoding.Default.GetBytes("?"));*/
                try
                {
                    buffers = new byte[2048];
                    int result = receiveSocket.Receive(buffers);
                    if (result == 0)
                    {
                        break;
                    }
                    else
                    {
                        str = Encoding.Default.GetString(buffers).Replace("\0", "");;
                        SetMessage("接收到服务器数据: start-------- " + str);
                        foreach (string line in str.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (!line.IsNullOrEmpty())
                            {
                                SetMessage("接收到服务器数据: line----------------------- " + line);
                                buffer.Enqueue(line);
                            }
                        }
                        SetMessage("接收到服务器数据: end---------------------------------" + str);
                        /*System.Threading.Thread.Sleep(1);*/
                    }

                }
                catch (Exception ex)
                {
                    SetMessage("StartReceive服务器异常:" + ex.Message);
                    Close(false);
                    /* if (!socket.Connected && !CanRead)
                     {
                         Open();
                     }*/

                }
            }

        }
        private void SetMessage(string msg)
        {
            Logger.LogMessage("SetMessage MKSLaserESP32", "Open {0}", msg);
        }

        private void Cln_OnOpen(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public string ReadLineBlocking()
        {
            string rv = null;
            while (IsOpen && rv == null) //wait for disconnect or data
            {
                if (buffer.Count > 0)
                    rv = buffer.Dequeue();
                else
                    System.Threading.Thread.Sleep(1);
            }

            /*ComLogger.Log("rx", rv);*/
            return rv;
        }

        public void Write(byte b)
        {
            if (IsOpen && CanRead)
            {
                SetMessage("服务器Write b:" + new string((char)b, 1));
                ComLogger.Log("tx", b);
                //socket.Send(System.Text.Encoding.Default.GetBytes(new string((char)b, 1)));
                socket.Send(new byte[] { b });
            }
        }

        public void Write(byte[] arr)
        {
            if (IsOpen && CanRead)
            {
                SetMessage("服务器Write arr:" + arr.ToString());
                ComLogger.Log("tx", arr);
                socket.Send(arr);
            }
        }

        public void Write(string text)
        {
            if (IsOpen)
            {
                if (!CanRead)
                {
                    MessageBox.Show("Busy now, please try again later.", Strings.WarnMessageBoxHeader);
                    return;
                }
                SetMessage("服务器Write text:" + text);
                ComLogger.Log("tx", text);
                socket.Send(System.Text.Encoding.Default.GetBytes(text));
                if (text.Contains("[ESP801]"))
                {
                    ComLogger.Log("", "Reading file...");
                    CanRead = false;
                }
            }
        }

        void cln_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Data != null)
            {
                foreach (string line in e.Data.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    buffer.Enqueue(line);
            }
        }
    }
}
