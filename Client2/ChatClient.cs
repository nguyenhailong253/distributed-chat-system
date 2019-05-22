using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Client2
{
    public class ChatClient
    {
        // Client socket.  
        private Socket _proxySocket;
        private Socket _serverSocket;
        // Size of receive buffer.  
        private const int BufferSize = 1024;

        private Packet _packetSent = new Packet();
        private Packet _packetReceived = new Packet();

        private static AutoResetEvent _connectDone =
            new AutoResetEvent(false);

        private static AutoResetEvent _chatDone = new AutoResetEvent(false);
        // The response from the remote device.  
        private string _stringReceived = null;
        private byte[] _bufferSent = new byte[1024];
        private byte[] _bufferReceived = new byte[BufferSize];

        private IPEndPoint _proxyEndPoint = null;
        private IPEndPoint _serverEndPoint = null;
        private bool _connectedToServer = false;
        private bool _doneChatting = false;
        private string _now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        private JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public void StartClient()
        {
            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.    
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                _proxyEndPoint = new IPEndPoint(ipAddress, 4000);
                _serverEndPoint = new IPEndPoint(ipAddress, 0000);

                // Create a socket to connect with server.
                _serverSocket = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Create a TCP/IP socket.  
                _proxySocket = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                while (true)
                {
                    try
                    {
                        // Trying to connect to proxy.
                        _proxySocket.Connect(_proxyEndPoint);
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                        Console.WriteLine("error connecting to proxy. Re-connecting...");
                    }
                }
                Console.WriteLine("Client connected to proxy {0}",
                    _proxySocket.RemoteEndPoint.ToString());

                while (true)
                {
                    _serverEndPoint = ProxyCommunicate();
                    Console.WriteLine("server ep: " + _serverEndPoint.ToString());
                    while (!_connectedToServer)
                    {
                        try
                        {
                            _serverSocket.Connect(_serverEndPoint);
                            _connectedToServer = true;
                        }
                        catch (Exception e)
                        {
                            // Connecting to the server failed (probs server is off)
                            // then request proxy for another server connection
                            Console.WriteLine(e.ToString());
                            _connectedToServer = false;
                            _serverEndPoint = ProxyCommunicate();
                        }
                    }
                    Console.WriteLine("Connecting to server...");
                    Thread sendThread = new Thread(Send);
                    sendThread.Name = "send";
                    sendThread.IsBackground = true;
                    sendThread.Start();

                    Thread receiveThread = new Thread(Receive);
                    receiveThread.Name = "receive";
                    receiveThread.IsBackground = true;
                    receiveThread.Start();

                    _connectDone.WaitOne();

                    _serverEndPoint = null;
                    _serverSocket.Shutdown(SocketShutdown.Both);
                    _serverSocket.Close();
                    if (_doneChatting)
                        break;
                }
                _chatDone.WaitOne();

                // Release the socket.  
                _proxySocket.Shutdown(SocketShutdown.Both);
                _proxySocket.Close();
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        private IPEndPoint createIPEndPoint(string IP)
        {
            // Reference:
            //https://stackoverflow.com/questions/2727609/best-way-to-create-ipendpoint-from-string
            Console.WriteLine("ip in create ip ep: " + IP);
            string ipAddress = IP.Substring(0, 30);
            string portNum = IP.Substring(31, IP.Length - ipAddress.Length - 1);
            Console.WriteLine(ipAddress);
            Console.WriteLine(portNum);
            IPAddress ip;
            int port;
            if ((IPAddress.TryParse(ipAddress, out ip)) && (int.TryParse(portNum, out port)))
            {
                Console.WriteLine("ip: " + ip + "port: " + port);
                return new IPEndPoint(ip, port);
            }
            else
                throw new FormatException("invalid IP and port");
        }

        private IPEndPoint ProxyCommunicate()
        {
            try
            {
                _packetSent.time = _now;
                _packetSent.IP = _proxySocket.LocalEndPoint.ToString();

                if (!_connectedToServer)
                {
                    // Preparing packet to send to proxy.
                    _packetSent.title = MsgTitle.connect_to_server.ToString();
                    _packetSent.content = null;
                    Console.WriteLine("Requesting to proxy for a server connection...");
                }
                else if (_packetReceived.title.Equals(MsgTitle.change_server.ToString()))
                {
                    _packetSent.title = MsgTitle.change_server.ToString();
                    _packetSent.content = _packetReceived.content;
                }
                // Serializing and send request to connect to a server.
                string serializedResult = _serializer.Serialize(_packetSent);
                _bufferSent = Encoding.ASCII.GetBytes(serializedResult);
                _proxySocket.Send(_bufferSent);

                // Ready to receive server endpoint info from proxy.
                _bufferReceived = new byte[2048];
                int bytesReceived = _proxySocket.Receive(_bufferReceived);

                // Deserializing packet from proxy.
                _stringReceived = Encoding.ASCII.GetString(_bufferReceived, 0, bytesReceived);
                var deserialized = _serializer.Deserialize<Packet>(_stringReceived);
                _packetReceived = deserialized;
                Console.WriteLine("Receiving server info...");

                // Generate endpoint for server.
                //IPEndPoint ep = createIPEndPoint(_packetReceived.content);
                return createIPEndPoint(_packetReceived.content);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        private void Receive()
        {
            bool done = false;
            try
            {
                while (!done)
                {
                    _bufferReceived = new byte[2048];
                    int bytesReceived = _serverSocket.Receive(_bufferReceived);

                    _stringReceived = Encoding.ASCII.GetString(_bufferReceived, 0, bytesReceived);

                    var serializer = new JavaScriptSerializer();
                    var serializedResult = serializer.Deserialize<Packet>(_stringReceived);

                    _packetReceived = serializedResult;
                    if (_packetReceived.title.Equals(MsgTitle.change_server.ToString()))
                    {
                        _connectDone.Set();
                        Console.WriteLine("Changing server...");
                        break;
                    }
                    Console.WriteLine(_packetReceived.content);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                done = true;
            }
        }

        private void Send()
        {
            bool done = false;
            string data;
            try
            {
                _packetSent.IP = _serverSocket.LocalEndPoint.ToString();
                while (!done)
                {
                    _bufferSent = new byte[2048];
                    Console.WriteLine("Type your message:");
                    if (_serverEndPoint != null)
                        data = Console.ReadLine();
                    else
                        break;

                    if (data.Equals("create chat room"))
                    {
                        _packetSent.title = MsgTitle.new_chatroom.ToString();
                        _packetSent.content = data;
                    }
                    else if (data.Equals("join chat room"))
                    {
                        _packetSent.title = MsgTitle.join_chatroom.ToString();
                        Console.WriteLine("Which chat room?");
                        _packetSent.content = Console.ReadLine();
                    }
                    else if (data.Equals("terminate"))
                    {
                        _packetSent.title = MsgTitle.terminate_user.ToString();
                        _packetSent.content = null;
                    }
                    else if (data.Equals("add user"))
                    {
                        _packetSent.title = MsgTitle.add_user.ToString();
                        Console.WriteLine("Which user?");
                        _packetSent.content = Console.ReadLine();
                    }
                    else if (data.Equals("exit"))
                    {
                        _packetSent.title = MsgTitle.exit_room.ToString();
                        _packetSent.content = null;
                    }
                    else if (data.Equals("kick user"))
                    {
                        _packetSent.title = MsgTitle.remove_user.ToString();
                        Console.WriteLine("Which user?");
                        _packetSent.content = Console.ReadLine();
                    }
                    else if (data.Equals("chat with"))
                    {
                        _packetSent.title = MsgTitle.chat_with_user.ToString();
                        Console.WriteLine("Which user?");
                        _packetSent.content = Console.ReadLine();
                    }
                    else
                    {
                        _packetSent.title = MsgTitle.chat_message.ToString();
                        _packetSent.content = data;
                    }
                    _packetSent.time = _now;
                    var serializer = new JavaScriptSerializer();

                    string serializedResult = serializer.Serialize(_packetSent);
                    _bufferSent = Encoding.ASCII.GetBytes(serializedResult);

                    _serverSocket.Send(_bufferSent);

                    if (data.Equals("terminate"))
                    {
                        _doneChatting = true;
                        _connectDone.Set();
                        Console.WriteLine("Disconnecting from Network....");
                        Thread.Sleep(3000);
                        _chatDone.Set();
                        System.Environment.Exit(1);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                done = true;
            }
        }
    }
}
