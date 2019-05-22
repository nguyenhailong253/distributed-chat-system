/// Author : long nguyen (nguyenhailong253@gmail.com)

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Proxy
{
    public class Proxy
    {
        /// <summary>
        ///
        /// This proxy will stand between client and server. It will be responsible
        /// for monitoring servers, directing clients to suitable server.
        /// 
        /// Proxy is bound to a local end point. Once accepted connection from
        /// a server, it establishes a new background thread to maintain
        /// the connection with that server. The communication between them
        /// are informing whether server is online or offline, updating server's
        /// current client list's length (how many clients it is serving), and heart
        /// beat (ping the server to see if it is still alive or not).
        /// Proxy also informs other connected servers of a new server online.
        /// Hence, all other servers can try to communicate with that new server.
        ///
        /// When received a request from a client, proxy will find the least busy
        /// server and send back the end point of that server to the client so the
        /// client can request to connect. 
        /// If the client requests the connect with user who is currently connecting
        /// to other server, client will send a change-server request to proxy and
        /// proxy will find that server and send back info to client.
        ///
        /// </summary>

        /// <rules>
        ///
        /// -  Client only connect to proxy to ask for server info or request to 
        /// change server.
        /// -  The connection between client and proxy is short connection. Once
        /// finished with the request, it closes the socket. If client has new 
        /// request, it has to reconnect with proxy.
        /// -  The connection between server and proxy is long connection. It 
        /// will remain until server is offline. 
        /// -  Proxy only holds servers info, it does not connect servers together.
        /// If servers want to connect with each other they will do it themselves.
        ///
        /// </rules>

        // Proxy has a list of online server.
        private Dictionary<string, ServerInfo> _serverDict = new Dictionary<string, ServerInfo>();

        // Proxy has 2 kinds of packets: send and receive.
        private Packet _packetSent = new Packet();
        private Packet _packetReceived = new Packet();

        // Data received in string type.
        private string dataReceived = null;

        // Communicator between threads.
        private AutoResetEvent _mainThread = new AutoResetEvent(false);
        private AutoResetEvent _receiveThread = new AutoResetEvent(false);

        // Setting current time.
        private string _now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        // Name of proxy.
        private string _name = "proxy";

        // Read-only property to access proxy name.
        public string ProxyName
        {
            get { return _name; }
        }

        // Local endpoint of proxy.
        private IPEndPoint _localEndPoint = null;

        // Read-only property to access proxy endpoint.
        public IPEndPoint ProxyEndPoint
        {
            get { return _localEndPoint; }
        }

        // Read-only property to access list of online servers.
        public Dictionary<string, ServerInfo> ServerDict
        {
            get { return _serverDict; }
        }

        // Method: finding the server least 'busy'.
        public ServerInfo LeastBusyServer()
        {
            int length = 100000;
            ServerInfo leastBusy = null;
            foreach (ServerInfo entry in _serverDict.Values)
            {
                if (entry.UserListLength < length)
                {
                    length = entry.UserListLength;
                    leastBusy = entry;
                }
            }
            return leastBusy;
        }

        // Method: broadcasting to other servers that a new server is on/off.
        public void BroadCastServer(ClientConnection s)
        {
            // If the request received is server_on
            if (_packetReceived.title.Equals(MsgTitle.server_on.ToString()))
            {
                // Create new instance of server and update its info.
                ServerInfo server = new ServerInfo();
                server.ServerEndPoint = _packetReceived.IP;
                server.ServerName = _packetReceived.sender;
                server.ServerConnection = s;

                // Add new server to proxy's server dict.
                _serverDict[_packetReceived.sender] = server;

                // Only broadcast if theres at least 2 servers in the list, 
                // if only 1 means that is the recently added server.
                if (_serverDict.Count >= 2)
                {
                    _packetSent.title = MsgTitle.server_on.ToString();

                    // Preparing packet to be sent to servers.
                    _packetSent.sender = this.ProxyName;
                    _packetSent.time = _now;
                    _packetSent.content = server.ServerName;
                    // IP in here will be the IP of the server
                    _packetSent.IP = server.ServerEndPoint;

                    // Broadcast to other servers about a newly online server.
                    foreach (var entry in _serverDict.Values)
                    {
                        // No need to broadcast to itself.
                        if (entry.ServerName != server.ServerName)
                        {
                            entry.ServerConnection.sendMsg(_packetSent);
                        }
                    }
                }
            }
            // If the request is server_off
            else if (_packetReceived.title.Equals(MsgTitle.server_off.ToString()))
            {
                if (_serverDict.Count >= 2)
                {
                    _packetSent.title = MsgTitle.server_off.ToString();

                    // Preparing packet to be sent to servers.
                    _packetSent.sender = this.ProxyName;
                    _packetSent.time = _now;
                    _packetSent.content = _packetReceived.sender;
                    _packetSent.IP = _packetReceived.IP;

                    // Broadcast to other servers about a newly offline server.
                    foreach (var entry in _serverDict.Values)
                    {
                        entry.ServerConnection.sendMsg(_packetSent);
                    }
                }
            }
        }

        // Method: sending message to servers and wait for response.
        public void HeartBeat()
        {
            // Ping server to check if it is still online
            // if message title is online then ok
            while (true)
            {
                // Ping every 5 mins = 300s = 300000ms
                Thread.Sleep(300000);
                foreach (var entry in _serverDict.Values)
                {
                    _packetSent.IP = _localEndPoint.ToString();
                    _packetSent.sender = ProxyName;
                    _packetSent.title = MsgTitle.are_you_online.ToString();
                    entry.ServerConnection.sendMsg(_packetSent);
                }
            }
        }

        // Method: Communication between server and proxy.
        // Executed on separated thread.
        public void ServerCommunicate(ClientConnection s)
        {
            bool notDone = true;
            try
            {
                // Begin receiving info from server.
                while (notDone)
                {
                    int bytesRec = s.ClientSocket.Receive(s.ReceiveBuffer);
                    string stringRec = Encoding.ASCII.GetString(s.ReceiveBuffer, 0, bytesRec);

                    // Deserializing the message.
                    var serializer = new JavaScriptSerializer();
                    var serializedResult = serializer.Deserialize<Packet>(stringRec);
                    _packetReceived = serializedResult;

                    string sender = _packetReceived.sender;

                    // When server update its client list,
                    // content will be the length
                    if (_packetReceived.title.Equals(MsgTitle.update_client_list.ToString()))
                    {
                        int listLength = 0;
                        if (int.TryParse(_packetReceived.content, out listLength))
                            _serverDict[sender].UserListLength = listLength;
                        Console.WriteLine("Server " + sender
                            + " update length of client list: " + listLength);
                    }
                    // or if server is going offline.
                    else if (_packetReceived.title.Equals(MsgTitle.server_off.ToString()))
                    {
                        if (_serverDict.ContainsKey(sender))
                        {
                            _serverDict.Remove(sender);
                        }
                        BroadCastServer(s);
                        Console.WriteLine("Server " + sender + " is offline");
                        break;
                    }
                    // If server responds to heart beat message.
                    else if (_packetReceived.title.Equals(MsgTitle.online.ToString()))
                        Console.WriteLine(sender + " is still online.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                notDone = false;
            }
        }

        // Method: handling request from client.
        // Executed on separated thread.
        public void RequestHandler(Object obj)
        {
            // Signalling the main thread to continue.
            _mainThread.Set();

            // Casting the type ClientConnection on obj
            ClientConnection client = (ClientConnection)obj;

            bool notDone = true;
            try
            {
                int bytesRec = 0;
                while (notDone)
                {
                    // Clear receive buffer.
                    client.ReceiveBuffer = new byte[1024];

                    // Receive a number of bytes of message.
                    bytesRec = client.ClientSocket.Receive(client.ReceiveBuffer);

                    // Convert bytes to string.
                    dataReceived = Encoding.ASCII.GetString(client.ReceiveBuffer, 0, bytesRec);

                    // Serialize the message to Packet type.
                    var serializer = new JavaScriptSerializer();
                    var serializedResult = serializer.Deserialize<Packet>(dataReceived);
                    _packetReceived = serializedResult;

                    // If packet received is from server trying to connect, add this 
                    // server to serverlist and update its information.
                    if (_packetReceived.title.Equals(MsgTitle.server_on.ToString()))
                    {
                        // "sender" will be the name of server. content received will be length of
                        // server's current user list. Then, proxy creates new instance of
                        // ServerInfo and update information for that instance based on packet received.
                        if (!_serverDict.ContainsKey(_packetReceived.sender))
                        {
                            BroadCastServer(client);
                        }
                        Console.WriteLine("Server IP is " + _packetReceived.IP);
                        Console.WriteLine("Server " + _packetReceived.sender + " is online");
                        // After informing other servers, break out of this loop to call another function
                        // that maintains connection with this new server.
                        break;
                    }

                    // If client request to connect to server,
                    if (_packetReceived.title.Equals(MsgTitle.connect_to_server.ToString()))
                    {
                        // Send back server info. content will be its IP endpoint, title will
                        // be server_info.
                        ServerInfo chosenServer = LeastBusyServer();
                        _packetSent.title = MsgTitle.server_info.ToString();
                        _packetSent.content = chosenServer.ServerEndPoint;
                        client.sendMsg(_packetSent);
                        Console.WriteLine("Client " + client.ClientSocket.RemoteEndPoint.ToString()
                            + " request for least busy server " + chosenServer.ServerName);
                        notDone = false;
                    }
                    // or if client request to chat with client connected to a different server,
                    else if (_packetReceived.title.Equals(MsgTitle.change_server.ToString()))
                    {
                        // loop server dict, check if it contains _packet.content which will be the name
                        // of other client/chatroom that user wants to join. then send back info of
                        // the server that has that info.
                        //Console.WriteLine("code have reached title = change_server");

                        string serverName = _packetReceived.content.Substring(0, 2);
                        //Console.WriteLine("wanted server name is " + serverName);
                        _packetSent.title = MsgTitle.server_info.ToString();
                        _packetSent.content = _serverDict[serverName].ServerEndPoint;
                        //Console.WriteLine("end point of wanted server: " + _packetSent.content);
                        client.sendMsg(_packetSent);
                        notDone = false;
                    }
                }
                // Exit this general 'request handler' function to enter another one
                // specialized with request from servers.
                if (_packetReceived.title.Equals(MsgTitle.server_on.ToString()))
                {
                    ServerCommunicate(client);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                notDone = false;
            }
        }

        // Method: proxy listening to request.
        public void StartListening()
        {
            // Establish the local endpoint for the socket
            // using the DNS name of the local computer.  
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            _localEndPoint = new IPEndPoint(ipAddress, 4000);

            // Create a TCP/IP socket to listen for connections.  
            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
            try
            {
                // Bind the socket to the local endpoint and 
                // listen for incoming connections.  
                listener.Bind(_localEndPoint);
                listener.Listen(1000);

                // Create a new thread just for checking servers' status.
                Thread checkThread = new Thread(HeartBeat);
                checkThread.Name = "check thread";
                checkThread.IsBackground = true;
                checkThread.Start();

                // Name main thread.
                Thread.CurrentThread.Name = "main thread";

                while (true)
                {
                    Console.WriteLine("Waiting for a connection...");

                    // Start an asynchronous socket to handle request.
                    // Program will pause here until request accepted.
                    Socket handler = listener.Accept();

                    // Create new instance of clientconnection object to
                    // hold information about new client. 
                    ClientConnection newConnection = new ClientConnection(handler);

                    // Handle client request on separated thread.
                    Thread clientThread = new Thread(RequestHandler);
                    clientThread.Start(newConnection);

                    // Set the thread to be running in background.
                    clientThread.IsBackground = true;

                    // This main thread will pause here until the client 
                    // thread send signal that it has created successfully.
                    _mainThread.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
        }
    }
}
