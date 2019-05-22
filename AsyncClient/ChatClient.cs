/// Author: long nguyen (nguyenhailong253@gmail.com)

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Web.Script.Serialization;

namespace AsyncClient
{
    /// <summary>
    /// 
    /// Client can connect to a server and start exchanging messages
    /// to other clients who are also connected to that server.
    /// 
    /// Client first connect to proxy and ask for information about an
    /// available server. After receiving the info, client establish 
    /// connection with that server. 
    /// 
    /// Connection between proxy and client is short connection.
    /// Connection between server and client is long connection.
    /// 
    /// Client will have 2 separated thread for sending and receiving
    /// messages so that they can see live messages without waiting.
    /// 
    /// </summary>
    
    public class ChatClient
    {
        // Client socket.  
        private Socket _proxySocket;
        private Socket _serverSocket;
        // Size of receive buffer.  
        private const int BufferSize = 1024;

        // Packet for sending and receiving data.
        private Packet _packetSent = new Packet();
        private Packet _packetReceived = new Packet();

        // Communicator between threads.
        private static AutoResetEvent _connectDone =
            new AutoResetEvent(false);
        private static AutoResetEvent _chatDone = 
            new AutoResetEvent(false);

        // The response from the remote device will be stored here.  
        private string _stringReceived = null;
        private byte[] _bufferSent = new byte[1024];
        private byte[] _bufferReceived = new byte[BufferSize];

        // Socket, end point information.
        private IPEndPoint _proxyEndPoint = null;
        private IPEndPoint _serverEndPoint = null;
        private IPAddress _ipAddress = null;
        
        // Current time.
        private string _now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        // Instance of JS serializer.
        private JavaScriptSerializer _serializer = new JavaScriptSerializer();

        // Variables controlling loops.
        private bool _connectedToServer = false;
        private bool _doneChatting = false;

        // Name of thread.
        private string oldSendThreadName = "send";
        private string oldReceiveThreadName = "receive";


        // Method: generate endpoint from the string received from proxy.
        private IPEndPoint createIPEndPoint(string IP)
        {
            // Reference:
            //https://stackoverflow.com/questions/2727609/best-way-to-create-ipendpoint-from-string
            // IP is the first 30 letters.
            string ipAddress = IP.Substring(0, 30);
            string portNum = IP.Substring(31, IP.Length - ipAddress.Length - 1);
            IPAddress ip;
            int port;
            if ((IPAddress.TryParse(ipAddress, out ip)) && (int.TryParse(portNum, out port)))
            {
                //Console.WriteLine("ip: " + ip + "port: " + port);
                return new IPEndPoint(ip, port);
            }
            else
                throw new FormatException("invalid IP and port");
        }

        // Method: communicating with proxy for server info.
        private IPEndPoint ProxyCommunicate()
        {
            try
            {
                _packetSent.time = _now;
                _packetSent.IP = _proxySocket.LocalEndPoint.ToString();
                //Console.WriteLine("packetRec title is " + _packetReceived.title);

                // If not currently connected to a server.
                if (!_connectedToServer)
                {
                    // Preparing packet to send to proxy.
                    _packetSent.title = MsgTitle.connect_to_server.ToString();
                    _packetSent.content = null;
                    Console.WriteLine("Requesting to proxy for a server connection...");
                }
                // If requesting to change to other server.
                else if (_packetReceived.title.Equals(MsgTitle.change_server.ToString()))
                {
                    // Preparing packet to send to proxy.
                    //Console.WriteLine("prepare to send to proxy");
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

                // Shutdown current socket after connecting with proxy.
                _proxySocket.Shutdown(SocketShutdown.Both);
                _proxySocket.Close();

                // Create new instance for later use.
                _proxySocket = new Socket(_ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Generate endpoint for server.
                return createIPEndPoint(_packetReceived.content);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        // Method: handling received message.
        // Executed on separated thread.
        private void Receive()
        {
            bool done = false;

            if (Thread.CurrentThread.Name == oldReceiveThreadName)
            {
                done = true;
            }
            int bytesReceived;
            try
            {
                while (!done)
                {
                    oldReceiveThreadName = Thread.CurrentThread.Name;
                    // Clear buffer and receive new message.
                    _bufferReceived = new byte[2048];
                    
                    bytesReceived = _serverSocket.Receive(_bufferReceived);
                    
                    
                    // Convert to string.
                    _stringReceived = Encoding.ASCII.GetString(_bufferReceived, 0, bytesReceived);

                    // Deserialize the message.
                    var serializer = new JavaScriptSerializer();
                    var serializedResult = serializer.Deserialize<Packet>(_stringReceived);
                    _packetReceived = serializedResult;

                    // If receive message saying change_server
                    if (_packetReceived.title.Equals(MsgTitle.change_server.ToString()))
                    {
                        // Finish connection with current server
                        _connectDone.Set();
                        Console.WriteLine("Changing server...");
                        _connectedToServer = false;
                        break;
                    }
                    Console.WriteLine(_packetReceived.content);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                //_doneChatting = true;
                done = true;
            }
        }

        // Method: handling sending message.
        // Executed on separated thread.
        private void Send()
        {
            bool done = false;
            if (Thread.CurrentThread.Name == oldSendThreadName)
            {
                done = true;
            }
            string data;
            try
            {
                while (!done)
                {
                    //Console.WriteLine("Currently in thread number: " + Thread.CurrentThread.Name);
                    Console.WriteLine("Type your message:");
                    // If server endpoint is available, meaning is connected
                    //Console.WriteLine("server ep: " + _serverEndPoint);
                    
                    data = Console.ReadLine();
                    if (Thread.CurrentThread.Name == oldSendThreadName)
                    {
                        break;
                    }

                    _packetSent.IP = _serverEndPoint.ToString();
                    // Clear buffer to send new ones.
                    _bufferSent = new byte[2048];
                    if (data.ToLower().Equals("create chat room"))
                    {
                        _packetSent.title = MsgTitle.new_chatroom.ToString();
                        _packetSent.content = data;
                    }
                    else if (data.ToLower().Equals("join chat room"))
                    {
                        _packetSent.title = MsgTitle.join_chatroom.ToString();
                        Console.WriteLine("Which chat room?");
                        _packetSent.content = Console.ReadLine();
                        _packetSent.title = MsgTitle.join_chatroom.ToString();
                    }
                    else if (data.ToLower().Equals("terminate"))
                    {
                        _packetSent.title = MsgTitle.terminate_user.ToString();
                        _packetSent.content = null;
                    }
                    else if (data.ToLower().Equals("add user"))
                    {
                        _packetSent.title = MsgTitle.add_user.ToString();
                        Console.WriteLine("Which user?");
                        _packetSent.content = Console.ReadLine();
                    }
                    else if (data.ToLower().Equals("exit"))
                    {
                        _packetSent.title = MsgTitle.exit_room.ToString();
                        _packetSent.content = null;
                    }
                    else if (data.ToLower().Equals("kick user"))
                    {
                        _packetSent.title = MsgTitle.remove_user.ToString();
                        Console.WriteLine("Which user?");
                        _packetSent.content = Console.ReadLine();
                    }
                    else if (data.ToLower().Equals("chat with"))
                    {
                        _packetSent.title = MsgTitle.chat_with_user.ToString();
                        Console.WriteLine("Which user?");
                        _packetSent.content = Console.ReadLine();
                    }
                    else
                    {
                        _packetSent.title = MsgTitle.chat_message.ToString();
                        _packetSent.content = data;
                        //Console.WriteLine("code is here now");
                    }
                    _packetSent.time = _now;
                    //Console.WriteLine("msg title: " + _packetSent.title);
                    // Serializing message.
                    var serializer = new JavaScriptSerializer();
                    string serializedResult = serializer.Serialize(_packetSent);

                    // Sending...
                    _bufferSent = Encoding.ASCII.GetBytes(serializedResult);
                 
                    _serverSocket.Send(_bufferSent);
                 
                    // If user wants to terminate.
                    if (data.Equals("terminate"))
                    {
                        // Finish chatting, finish connection, shut down program.
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
                //_doneChatting = true;
                done = true;
            }
        }

        // Method: client starts to connect to proxy, then server.
        public void StartClient()
        {
            int i = 0;
            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.    
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                _ipAddress = ipHostInfo.AddressList[0];
                _proxyEndPoint = new IPEndPoint(_ipAddress, 4000);
                _serverEndPoint = new IPEndPoint(_ipAddress, 0000);

                // Create a socket to connect with server.
                _serverSocket = new Socket(_ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Create a TCP/IP socket.  
                _proxySocket = new Socket(_ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                while (true)
                {
                    // Waiting to connect to proxy. If failed, try again
                    // until successfull.
                    while (true)
                    {
                        try
                        {
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

                    // Get the server information and pass into server endpoint variable.
                    _serverEndPoint = ProxyCommunicate();
                    Console.WriteLine("Connected to: " + _serverEndPoint.ToString());

                    // Waiting to connect to server. Keep trying until successful.
                    while (!_connectedToServer)
                    {
                        try
                        {
                            _serverSocket.Connect(_serverEndPoint);
                            _connectedToServer = true;
                            //Console.WriteLine("socket connected");
                            //Console.WriteLine("Connected");
                        }
                        catch (Exception e)
                        {
                            // Connecting to the server failed (probs server is off)
                            // then request proxy for another server connection
                            Console.WriteLine(e.ToString());
                            _connectedToServer = false;
                            _serverEndPoint = ProxyCommunicate();
                            //Console.WriteLine("Connected");
                        }
                    }
                    //Console.WriteLine("Connected to server...");

                    // Create new background thread for sending message.
                    Thread sendThread = new Thread(Send);
                    sendThread.Name = "send " + i;
                    sendThread.IsBackground = true;
                    sendThread.Start();
                    
                    // Create new background thread for receiving message.
                    Thread receiveThread = new Thread(Receive);
                    receiveThread.Name = "receive " + i;
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                    i++;

                    // This main thread will wait here until those 2 threads
                    // send signal that they are done.
                    _connectDone.WaitOne();
                    oldSendThreadName = sendThread.Name;
                    oldReceiveThreadName = receiveThread.Name;
                    //Console.WriteLine("Finished with this server: " + _serverEndPoint.ToString());

                    // Reseting the server end point/socket.
                    //_serverEndPoint = null;
                    //_serverSocket.Shutdown(SocketShutdown.Both);
                    //_serverSocket.Close();
                    _serverSocket = new Socket(_ipAddress.AddressFamily,
                        SocketType.Stream, ProtocolType.Tcp);

                    // If client is finished chatting and want to terminate.
                    if (_doneChatting)
                        break;

                    //Console.WriteLine("Loop and communicate with proxy again");
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
    }
}
