/// Author : long nguyen (nguyenhailong253@gmail.com)

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using AsyncServer.MultiServices;

namespace AsyncServer
{
    /// <summary>
    ///
    /// An asynchronous server which handles clients' requests asynchronously 
    /// using multithreading. Usually using 1 separated thread to handle client request
    /// 
    /// Client thread will be closed when client specifies "terminate" command
    /// or is suddenly disconnected.
    ///
    /// What server can do includes:
    /// - Create chat room.
    /// - Allow user to join existing chat room.
    /// - User 1 request to add other user to chat room.
    /// - Exit chat room.
    /// - Terminate user.
    /// - Allow users in same chat room to exchange messages.
    ///
    /// Servers in this network can talk to each other. Each peer server connection
    /// will be maintained on a separated thread. Sever thread only closed when
    /// one of the servers specify command "server off" or suddenly disconnected.
    ///
    /// If client requests to chat with user currently connecting to a different
    /// server, send back a message called change_server to that client. The client
    /// will direct it to proxy and proxy will find a server suitable for it.
    ///
    /// All servers directly report to a Proxy about their status and their 
    /// client lists.
    ///
    /// </summary>

    /// <rules>
    ///
    /// -  One client can only be in 1 chat room at a time.
    /// -  There is no room admin.
    /// -  Chat room is NOT eliminated when there are no users in it.
    /// -  Client with no CurrentRoom property will be in MainHall.
    ///
    /// </rules>

    /// <convention>
    ///
    /// -  Server name will start with "S" followed by a number.
    /// eg: S1, S2.
    /// -  Client name will start with server name followed by "C" and a number.
    /// eg: S1C1, S2C3.
    /// -  Room name will start with server name followed by "R" and a number.
    /// eg: S1R2, S3R4.
    ///
    /// </convention>

    public class ChatServer
    {
        /*
         *  ============= ATTRIBUTES ============
         */

        // Server knows its list of users
        // and list of current chat room available.
        private List<UserInfo> _userList = new List<UserInfo>();
        private List<ChatRoom> _localCR = new List<ChatRoom>();

        public List<UserInfo> ServerUserList
        {
            get { return _userList; }
        }

        public List<ChatRoom> LocalChatRoom
        {
            get { return _localCR; }
            set { _localCR = value; }
        }

        // Server knows chat rooms of other servers in network
        // and list of remote users.
        // Server knows chat rooms of other servers in network
        // and list of remote users.
        // Key = server name, list = list of chat rooms/users
        private Dictionary<string, List<string>> _peerChatrooms = new Dictionary<string, List<string>>();
        private Dictionary<string, List<string>> _peerUsers = new Dictionary<string, List<string>>();

        public Dictionary<string, List<string>> PeerChatRooms
        {
            get { return _peerChatrooms; }
            set { _peerChatrooms = value; }
        }

        public Dictionary<string, List<string>> PeerUsers
        {
            get { return _peerUsers; }
            set { _peerUsers = value; }
        }

        // Key = chat room name, list = list of client in chat room
        private Dictionary<string, List<string>> _peerClientInCR = new Dictionary<string, List<string>>();

        public Dictionary<string, List<string>> PeerClientInCR
        {
            get { return _peerClientInCR; }
            set { _peerClientInCR = value; }
        }

        // Key = name of server, client connection = connection with that server.
        // This will not include THIS server. Hence, total servers will be length of this dict + 1
        private Dictionary<string, ClientConnection> _peerServerDict = new Dictionary<string, ClientConnection>();

        public Dictionary<string, ClientConnection> PeerServerDict
        {
            get { return _peerServerDict; }
            set { _peerServerDict = value; }
        }

        // Main hall with all users without current chat room.
        private MainHall _mainHall = new MainHall();

        public MainHall MainHall
        {
            get { return _mainHall; }
            set { _mainHall = value; }
        }

        // Main buffer of server for 
        // containing sent or received data.
        //private byte[] _mainBuffer = new byte[1024];

        // Data received in string type.
        private string dataReceived = null;

        // Server has 2 kinds of packets: send and receive.
        private Packet _packetSent = new Packet();
        private Packet _packetReceived = new Packet();

        // Properties to access 2 packets.
        public Packet PacketSent
        {
            get { return _packetSent; }
            set { _packetSent = value; }
        }

        public Packet PacketReceived
        {
            get { return _packetReceived; }
            set { _packetReceived = value; }
        }

        // Number of clients/chat rooms
        private int _clientCount = 0;
        private int _chatRoomCount = 0;

        // Communicator between threads.
        private AutoResetEvent _mainThread = new AutoResetEvent(false);
        private AutoResetEvent _receiveThread = new AutoResetEvent(false);

        // Setting current time.
        private string _now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        public string Now
        {
            get { return _now; }
        }

        // Variable controlling whether chat finished.
        private bool _finishedChatting = false;

        public bool FinishedChatting
        {
            get { return _finishedChatting; }
            set { _finishedChatting = value; }
        }

        // ClientConnection of server for communication between servers.
        //private ClientConnection _serverConnection = null;

        // ClientConnection of communication between proxy and this server.
        private ClientConnection _proxyConnection = null;

        public ClientConnection ProxyConnection
        {
            get { return _proxyConnection; }
            set { _proxyConnection = value; }
        }

        // Remote endpoint of other servers/proxy.
        private IPHostEntry _ipHostInfo = null;
        private IPAddress _ipAddress = null;
        private IPEndPoint _proxyEndPoint = null;

        // Name of server.
        private string _name = "S1";

        // Read-only property to access server name.
        public string ServerName
        {
            get { return _name; }
        }

        // Local endpoint of server.
        private IPEndPoint _localEndPoint = null;

        // Read-only property to access server endpoint.
        public IPEndPoint ServerEndPoint
        {
            get { return _localEndPoint; }
        }

        // Services.
        private Services services;


        /*
         * =============== METHODS ===============
         */

        private IPEndPoint createIPEndPoint(string IP)
        {
            // Reference:
            //https://stackoverflow.com/questions/2727609/best-way-to-create-ipendpoint-from-string
            string ipAddress = IP.Substring(0, 30);
            string portNum = IP.Substring(31, IP.Length - ipAddress.Length - 1);
            IPAddress ip;
            int port;
            if ((IPAddress.TryParse(ipAddress, out ip)) && (int.TryParse(portNum, out port)))
            {
                Console.WriteLine("Got ip: " + ip + " port: " + port);
                return new IPEndPoint(ip, port);
            }
            else
                throw new FormatException("invalid IP and port");
        }

        // Method for checking socket is still connected or not.
        // Using heartbeat concept: send message to poll of sockets
        // and wait for response. If false, socket disconnected.
        public bool SocketConnected(Socket s)
        {
            bool part1 = s.Poll(1000, SelectMode.SelectRead);
            bool part2 = (s.Available == 0);
            if (part1 && part2)
                return false;
            else
                return true;
        }

        // Method: testing client status
        // Looping through server's client list 
        // and check if client socket is still connected. 
        // If not, remove from client list.
        public void TestClientStatus()
        {
            while (true)
            {
                List<UserInfo> tempList = new List<UserInfo>(_userList);

                foreach (UserInfo user in tempList)
                {
                    if (!SocketConnected(user.ClientConnection.ClientSocket))
                    {
                        Console.WriteLine("User disconnected: {0}", user.UserName);
                        _userList.Remove(user);
                        Console.WriteLine("Updated Client List: ");
                        foreach (UserInfo u in _userList)
                        {
                            Console.WriteLine(u.UserName);
                        }
                    }
                }
                Thread.Sleep(1000);
            }
        }
        
        // Method: maintaining communication with proxy.
        // Executed on separated thread.
        private void ProxyCommunicate()
        {
            // Inform proxy that this server is on.
            _packetSent.title = MsgTitle.server_on.ToString();
            _packetSent.time = _now;

            // use local EP of this server so that proxy can send this EP
            // to other servers, then they can connect to this server.
            _packetSent.IP = _localEndPoint.ToString();
            _packetSent.content = null;
            _packetSent.sender = this.ServerName;

            _proxyConnection.sendMsg(_packetSent);
            Console.WriteLine("proxy is online");

            bool notDone = true;
            try
            {
                // Begin receiving info from proxy.
                while (notDone)
                {
                    int bytesRec = _proxyConnection.ClientSocket.Receive(_proxyConnection.ReceiveBuffer);
                    string stringRec = Encoding.ASCII.GetString(_proxyConnection.ReceiveBuffer, 0, bytesRec);

                    var serializer = new JavaScriptSerializer();
                    var serializedResult = serializer.Deserialize<Packet>(stringRec);
                    _packetReceived = serializedResult;
                    Console.WriteLine(_packetReceived.content);
                    Console.WriteLine(_packetReceived.IP);
                    Console.WriteLine(_packetReceived.sender);
                    Console.WriteLine(_packetReceived.title);

                    // When proxy is checking whether this server is still online.
                    if (_packetReceived.title.Equals(MsgTitle.are_you_online.ToString()))
                    {
                        // Inform proxy that this server is still online.
                        _packetSent.title = MsgTitle.online.ToString();
                        _packetSent.time = _now;
                        _packetSent.IP = _proxyConnection.ClientSocket.LocalEndPoint.ToString();
                        _packetSent.content = "online";
                        _packetSent.sender = this.ServerName;

                        _proxyConnection.sendMsg(_packetSent);
                    }
                    // Proxy inform that there is a new server online.
                    else if (_packetReceived.title.Equals(MsgTitle.server_on.ToString()))
                    {
                        // content will be name of server online
                        // IP is the IP of that server.
                        // create new thread and call the servercommunicate function
                        // create/update server ClientConnection
                        Console.WriteLine("sender is " + _packetReceived.sender);

                        // New socket to connect with proxy.
                        Socket serverSocket = new Socket(_ipAddress.AddressFamily,
                            SocketType.Stream, ProtocolType.Tcp);
                        // Generate endpoint for server.
                        IPEndPoint remoteEP = createIPEndPoint(_packetReceived.IP);
                        serverSocket.Connect(remoteEP);
                        // Create new instance of ClientConnection to hold info.
                        ClientConnection newServerConn = new ClientConnection(serverSocket);
                        UserInfo serverInfo = new UserInfo(newServerConn);
                        newServerConn.GetUser(serverInfo);
                        newServerConn.Name = _packetReceived.content;

                        // Add this server to server dict.
                        Console.WriteLine("content " + _packetReceived.content);
                        _peerServerDict[_packetReceived.content] = newServerConn;

                        // Make a new thread to maintain servers connection.
                        Thread serverThread = new Thread(ServerCommunicate);
                        serverThread.Name = newServerConn.Name;
                        Console.WriteLine(serverThread.Name);
                        serverThread.IsBackground = true;
                        serverThread.Start(newServerConn);
                    }
                    else if (_packetReceived.title.Equals(MsgTitle.server_off.ToString()))
                    {
                        // Content is the name of the server.
                        if (_peerServerDict.ContainsKey(_packetReceived.content))
                        {
                            _peerServerDict.Remove(_packetReceived.content);
                            Console.WriteLine("Server " + _packetReceived.content + " is offline");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                notDone = false;
            }
        }

        // Method: maintaining communication with other server.
        // Executed on separated thread.
        private void ServerCommunicate(Object obj)
        {
            ClientConnection s = (ClientConnection)obj;

            // First thing to do after successfully connect to that server:
            // Inform the other server that this server is on, along with info
            _packetSent.title = MsgTitle.server_on.ToString();
            _packetSent.time = _now;
            _packetSent.IP = _localEndPoint.ToString();
            _packetSent.content = null;
            _packetSent.sender = this.ServerName;

            s.sendMsg(_packetSent);
            Console.WriteLine("Server " + s.Name + " is online");

            bool notDone = true;
            try
            {
                // Begin receiving info from other server.
                while (notDone)
                {
                    _packetReceived.title = MsgTitle.connect_to_server.ToString();
                    int bytesRec = s.ClientSocket.Receive(s.ReceiveBuffer);
                    string stringRec = Encoding.ASCII.GetString(s.ReceiveBuffer, 0, bytesRec);

                    var serializer = new JavaScriptSerializer();
                    var serializedResult = serializer.Deserialize<Packet>(stringRec);
                    _packetReceived = serializedResult;

                    string sender = _packetReceived.sender;

                    // When server has new client
                    if (_packetReceived.title.Equals(MsgTitle.add_client.ToString()))
                    {
                        if (!_peerUsers.ContainsKey(sender))
                        {
                            _peerUsers[sender] = new List<string>();
                        }
                        _peerUsers[sender].Add(_packetReceived.content);
                        Console.WriteLine("Server " + sender
                            + " add new client: " + _packetReceived.content);
                    }
                    // or server removes one client,
                    else if (_packetReceived.title.Equals(MsgTitle.remove_client.ToString()))
                    {
                        _peerUsers[sender].Remove(_packetReceived.content);
                        Console.WriteLine("Server " + sender
                           + " remove client: " + _packetReceived.content);
                    }
                    // or server has a new chat room,
                    else if (_packetReceived.title.Equals(MsgTitle.add_chatroom.ToString()))
                    {
                        if (!_peerChatrooms.ContainsKey(sender))
                        {
                            _peerChatrooms[sender] = new List<string>();
                        }
                        _peerChatrooms[sender].Add(_packetReceived.content);
                        Console.WriteLine("Server " + sender
                           + " add new chat room: " + _packetReceived.content);
                    }
                    // or server removes a chat room,
                    else if (_packetReceived.title.Equals(MsgTitle.remove_chatroom.ToString()))
                    {
                        _peerChatrooms[sender].Remove(_packetReceived.content);
                        Console.WriteLine("Server " + sender
                           + " remove chat room: " + _packetReceived.content);
                    }
                    // or theres a new client to a chat room, 
                    else if (_packetReceived.title.Equals(MsgTitle.client_to_chatroom.ToString()))
                    {
                        string[] content = _packetReceived.content.Split();
                        // content[0] = room name,
                        // content[1] = client name.
                        if (!_peerClientInCR.ContainsKey(content[0]))
                        {
                            _peerClientInCR[content[0]] = new List<string>();
                        }
                        _peerClientInCR[content[0]].Add(content[1]);
                        Console.WriteLine("Server " + sender
                           + " add client " + content[1] + " to chat room: " + content[0]);
                    }
                    // or theres a client leaving chat room,
                    else if (_packetReceived.title.Equals(MsgTitle.client_outof_chatroom.ToString()))
                    {
                        string[] content = _packetReceived.content.Split();
                        // content[0] = room name,
                        // content[1] = client name.
                        _peerClientInCR[content[0]].Remove(content[1]);
                        Console.WriteLine("Server " + sender
                           + " remove client " + content[1] + " to chat room: " + content[0]);
                    }
                    // or if server is on,
                    else if (_packetReceived.title.Equals(MsgTitle.server_on.ToString()))
                    {
                        if (!_peerServerDict.ContainsKey(sender))
                        {
                            _peerServerDict[sender] = s;
                            Console.WriteLine("Server " + sender + " is online");
                        }
                    }
                    // or if server is off.
                    else if (_packetReceived.title.Equals(MsgTitle.server_off.ToString()))
                    {
                        if (_peerServerDict.ContainsKey(sender))
                        {
                            _peerServerDict.Remove(sender);
                            Console.WriteLine("Server " + sender + " is offline");
                        }
                        _peerChatrooms.Remove(sender);
                        _peerUsers.Remove(sender);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                notDone = false;
            }
        }

        // Method: Receive and process the message.
        // Executed on a separated thread for one particular client.
        public void RequestHandler(Object obj)
        {
            // Signalling the main thread to continue.
            _mainThread.Set();

            // Casting the type ClientConnection on ar.
            ClientConnection client = (ClientConnection)obj;
            try
            {
                // If there r other servers in network, 
                // and this is not a request from another server,
                // inform them & proxy about newly connected client
                Console.WriteLine("serverdict count = " + _peerServerDict.Count);
                Console.WriteLine("msg title = " + _packetReceived.title);
                if ((_peerServerDict.Count != 0) && (_packetReceived.title != MsgTitle.server_on.ToString()))
                {
                    _packetSent.title = MsgTitle.add_client.ToString();
                    _packetSent.content = client.ThisUser.UserName;
                    _packetSent.IP = client.ClientSocket.LocalEndPoint.ToString();
                    _packetSent.time = _now;
                    _packetSent.sender = this.ServerName;

                    // Inform other servers.
                    foreach (var entry in _peerServerDict.Values)
                        entry.sendMsg(_packetSent);

                    // Inform proxy.
                    _packetSent.title = MsgTitle.update_client_list.ToString();
                    _packetSent.content = ServerUserList.Count.ToString();
                    _proxyConnection.sendMsg(_packetSent);
                }

                int bytesRec = 0;

                while (true)
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
                    //_packetReceived = new Packet();
                    //_packetSent = new Packet();
                    _packetReceived = serializedResult;

                    // If packet received is from server, break from this loop to
                    // call another function for communication between servers.
                    if (_packetReceived.title.Equals(MsgTitle.server_on.ToString()))
                    {
                        // Update name of server connection object to name of server.
                        client.Name = _packetReceived.sender;
                        _userList.Remove(client.ThisUser);
                        _mainHall.RemoveUser(client.ThisUser);
                        Console.WriteLine("got a mess from " + client.Name);
                        // Add this server to server dict.
                        _peerServerDict[_packetReceived.sender] = client;

                        if (!_peerUsers.ContainsKey(_packetReceived.sender))
                        {
                            _peerUsers[_packetReceived.sender] = new List<string>();
                        }
                        break;
                    }

                    // If message title is create new chat room,
                    if (_packetReceived.title.Equals(MsgTitle.new_chatroom.ToString()))
                    {
                        services = new CreateChatRoom(this, client);
                        services.Serve();
                        //CreateChatRoom(client);
                    }
                    // or join chat room,
                    else if (_packetReceived.title.Equals(MsgTitle.join_chatroom.ToString()))
                    {
                        services = new JoinChatRoom(this, client);
                        services.Serve();
                        //JoinChatroom(client);
                    }
                    // or request to add other user to chat room
                    else if (_packetReceived.title.Equals(MsgTitle.add_user.ToString()))
                    {
                        services = new AddUserToRoom(this, client);
                        services.Serve();
                        //AddUserToRoom(client);
                    }
                    // or client want to terminate connection with server,
                    else if (_packetReceived.title.Equals(MsgTitle.terminate_user.ToString()))
                    {
                        services = new TerminateClient(this, client);
                        services.Serve();
                        //TerminateClient(client);
                    }
                    // or client want to exit current chat room,
                    else if (_packetReceived.title.Equals(MsgTitle.exit_room.ToString()))
                    {
                        services = new ExitRoom(this, client);
                        services.Serve();
                        //ExitRoom(client);
                    }
                    // or one user want to remove other user from chat room,
                    else if (_packetReceived.title.Equals(MsgTitle.remove_user.ToString()))
                    {
                        services = new KickUser(this, client);
                        services.Serve();
                        //KickUser(client);
                    }
                    // or user request to chat with other user.
                    else if (_packetReceived.title.Equals(MsgTitle.chat_with_user.ToString()))
                    {
                        services = new JoinUser(this, client);
                        services.Serve();
                        //JoinUser(client);
                    }
                    // or just simply chatting.
                    else
                    {
                        services = new Chatting(this, client);
                        services.Serve();
                        //Chatting(client);
                    }

                    // If finished chatting is set to true, break the loop.
                    if (_finishedChatting)
                    {
                        break;
                    }
                }
                // If message is from another server, call function for communication
                // between servers.
                if (_packetReceived.title.Equals(MsgTitle.server_on.ToString()))
                {
                    ServerCommunicate(client);
                }
            }
            catch (Exception e)
            {
                TerminateClient(client);
                Console.WriteLine(e.ToString());
            }
        }


        // Method: server listening to client request.
        public void StartListening()
        {
            // Establish the local endpoint for the socket
            // using the DNS name of the local computer.  
            _ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            _ipAddress = _ipHostInfo.AddressList[0];
            _localEndPoint = new IPEndPoint(_ipAddress, 3000);
            // Proxy endpoint.
            _proxyEndPoint = new IPEndPoint(_ipAddress, 4000);

            // New socket to connect with proxy.
            Socket proxySocket = new Socket(_ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Create a TCP/IP socket to listen for connections.  
            Socket listener = new Socket(_ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            try
            {
                // Bind the socket to the local endpoint and 
                // listen for incoming connections.  
                listener.Bind(_localEndPoint);
                listener.Listen(100);

                // Create a new thread just for checking clients' status.
                Thread checkThread = new Thread(TestClientStatus);
                checkThread.Name = "check thread";
                checkThread.IsBackground = true;
                checkThread.Start();

                // Name main thread.
                Thread.CurrentThread.Name = "main thread";

                // Connect with proxy
                proxySocket.Connect(_proxyEndPoint);
                _proxyConnection = new ClientConnection(proxySocket);

                // Make a new thread to maintain proxy connection.
                Thread proxyThread = new Thread(ProxyCommunicate);
                proxyThread.Name = "proxy thread";
                proxyThread.IsBackground = true;
                proxyThread.Start();

                // Add main hall to list of chat room.
                _localCR.Add(_mainHall);
                while (true)
                {
                    Console.WriteLine("Waiting for a connection...");

                    // Start an asynchronous socket to handle client request.
                    // Program will pause here until request accepted.
                    Socket handler = listener.Accept();

                    // Once accepted, create a ClientConnection object
                    // and a UserInfo object to contain that particular 
                    // user information.
                    ClientConnection clientState = new ClientConnection(handler);
                    UserInfo newUser = new UserInfo(clientState);

                    // Assign name for connected client.
                    newUser.UserName = "S1C" + _clientCount.ToString();
                    clientState.GetUser(newUser);

                    // Update server's client list.
                    _userList.Add(newUser);

                    // Add user to main hall.
                    _mainHall.AddUser(newUser);

                    // Update user's current room = main hall.
                    newUser.CurrentChatRoom = _mainHall;

                    _clientCount++;

                    // Create new thread to asynchronously handle client request.
                    Thread clientThread = new Thread(RequestHandler);
                    clientThread.Start(clientState);

                    // Name the thread after client name.
                    clientThread.Name = newUser.UserName;
                    // Set the thread to be running in background.
                    clientThread.IsBackground = true;

                    // This thread (main thread) will pause here until the client 
                    // thread created above signal that it has created successfully.
                    _mainThread.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                // Informing proxy that this server is going offline.
                _packetSent.title = MsgTitle.server_off.ToString();
                _packetSent.sender = this.ServerName;
                _packetSent.content = null;

                foreach (var entry in _peerServerDict.Values)
                {
                    _packetSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(_packetSent);
                }
                _proxyConnection.sendMsg(_packetSent);
                // Shutdown listening socket.
                listener.Shutdown(SocketShutdown.Both);
                listener.Close();
            }
            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
        }
    }
}


/// Author: long nguyen (nguyenhailong253@gmail.com)

using System.Net.Sockets;
using System.Text;
using System.Web.Script.Serialization;

namespace AsyncServer
{
    /// <summary>
    ///
    /// This class models the connection between server and one client.
    /// It contains attributes (connection socket), methods related to 
    /// establishing, receiving, storing, sending packets of data.
    ///
    /// </summary>
    public class ClientConnection
    {
        // Size of this connection buffer.  
        public const int BufferSize = 1024;

        // Client socket.  
        private Socket _handleSocket;

        // User who relates to this socket.
        private UserInfo _thisUser = null;

        // Buffer for temporarily storing data.  
        private byte[] _SendBuffer;
        private byte[] _ReceiveBuffer;

        public ClientConnection(Socket socket)
        {
            _handleSocket = socket;
            _SendBuffer = new byte[BufferSize];
            _ReceiveBuffer = new byte[BufferSize];
        }

        public string Name
        {
            get { return ThisUser.UserName; }
            set { ThisUser.UserName = value; }
        }

        public byte[] SendBuffer
        {
            get { return _SendBuffer; }
            set { _SendBuffer = value; }
        }

        public byte[] ReceiveBuffer
        {
            get { return _ReceiveBuffer; }
            set { _ReceiveBuffer = value; }
        }

        public Socket ClientSocket
        {
            get { return _handleSocket; }
            set { _handleSocket = value; }
        }

        // Get the User who relates to this connection.
        public void GetUser(UserInfo user)
        {
            _thisUser = user;
        }

        public UserInfo ThisUser
        {
            get { return _thisUser; }
        }

        // Sending message from server to the connected client.
        public void sendMsg(Packet packet)
        {
            // Serializing the data before sending.
            var serializer = new JavaScriptSerializer();
            string serializedResult = serializer.Serialize(packet);
            _SendBuffer = Encoding.ASCII.GetBytes(serializedResult);

            _handleSocket.Send(_SendBuffer);
            // Clear the buffer.
            _SendBuffer = new byte[BufferSize];
        }
    }
}

/// Author : long nguyen (nguyenhailong253@gmail.com)

using System.Collections.Generic;

namespace AsyncServer
{
    /// <summary>
    ///
    /// Chat room is where users exchange message.
    /// It contains list of users in chat room, 
    /// which server it belongs to and its name.
    ///
    /// </summary>
    public abstract class ChatRoom
    {
        private string _chatroomName = null;
        private List<UserInfo> _userList = new List<UserInfo>();
        private ChatServer _server = null;

        public ChatServer ServerInCharge
        {
            get { return _server; }
            set { _server = value; }
        }

        public string RoomName
        {
            get { return _chatroomName; }
            set { _chatroomName = value; }
        }

        public List<UserInfo> UserList
        {
            get { return _userList; }
            set { _userList = value; }
        }

        public void AddUser(UserInfo user)
        {
            _userList.Add(user);
        }

        public void RemoveUser(UserInfo user)
        {
            _userList.Remove(user);
        }

        public bool UserInRoom(UserInfo user)
        {
            if (UserList.Contains(user))
                return true;
            return false;
        }
    }
}

// Author : long nguyen (nguyenhailong253@gmail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class LocalChatRoom : ChatRoom
    {
        public LocalChatRoom(ChatServer server, string name, UserInfo owner)
        {
            ServerInCharge = server;
            RoomName = name;

            AddUser(owner);
        }
    }
}


/// Author : long nguyen (nguyenhailong253@gmail.com)

namespace AsyncServer
{
    /// <summary>
    ///
    /// Packet is the container of messages exchanged between
    /// servers, proxy, clients. Packet contains information 
    /// about:
    /// -  title: purpose of the request
    /// -  time: time the request is sent
    /// -  IP: local end point of the sender
    /// -  sender: name of the sender
    /// -  content: actual content of the message
    ///
    /// </summary>

    public enum MsgTitle
    {
        // client to server
        new_chatroom,
        new_user,
        terminate_user,
        join_chatroom,
        confirm_created,
        confirm_joined,
        add_user,
        chat_message,
        exit_room,
        add_user_fail,
        add_user_success,
        remove_user,
        chat_with_user,
        // server to server
        add_client,
        remove_client,
        add_chatroom,
        remove_chatroom,
        client_to_chatroom, // content = ["clientname", "roomname"]
        client_outof_chatroom,
        server_on,
        server_off,
        // server to proxy
        are_you_online,
        online,
        update_client_list,
        // client to proxy
        connect_to_server,
        server_info,
        change_server,
    };
    public class Packet
    {
        public string sender { get; set; }
        public string title { get; set; }
        public string IP { get; set; }
        public string content { get; set; }
        public string time { get; set; }
    }
}


/// Author : long nguyen (nguyenhailong253@gmail.com)

using System;

namespace AsyncServer
{
    /// <summary>
    /// 
    /// UserInfo contains information about user like 
    /// name, current chat room, its instance of client-
    /// connection
    /// 
    /// </summary>
    public class UserInfo
    {
        private string _userName;
        private ChatRoom _currentRoom;
        private ClientConnection _connection;

        public UserInfo(ClientConnection connection)
        {
            _userName = null;
            _connection = connection;
            _currentRoom = null;
        }

        public string UserName
        {
            get { return _userName; }
            set { _userName = value; }
        }

        public ChatRoom CurrentChatRoom
        {
            get { return _currentRoom; }
            set { _currentRoom = value; }
        }

        public ClientConnection ClientConnection
        {
            get { return _connection; }
            set { _connection = value; }
        }
    }
}


/// Author: long nguyen (nguyenhailong253@gmail.com)

using System;

namespace AsyncServer
{
    /// <summary>
    /// 
    /// Main hall is where all clients without chat room belong.
    /// When client first connected to server, it will be put
    /// in to main hall
    /// 
    /// </summary>
    public class MainHall : ChatRoom
    {
        public MainHall()
        {
            RoomName = "MainHall";
        }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public abstract class Services
    {
        private ClientConnection _client;
        private int _chatRoomCount = 0;
        private ChatServer _server;

        public Services(ChatServer server, ClientConnection client)
        {
            _client = client;
            _server = server;
        }

        public ChatServer Server
        {
            get { return _server; }
        }

        public ClientConnection ClientConn
        {
            get { return _client; }
        }

        public int ChatRoomCount
        {
            get { return _chatRoomCount; }
            set { _chatRoomCount = value; }
        }

        public abstract void Serve();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class AddUserToRoom : Services
    {
        public AddUserToRoom(ChatServer server, ClientConnection client) : base(server, client)
        {
            //
        }

        // Method: Request to add other user to room.
        public override void Serve()
        {
            // The title of the received packet will be request to add user to room.
            // The content of the received packet will be the name of the added user.

            // Preparing packet to be sent.
            Server.PacketSent.time = Server.Now;
            Server.PacketSent.IP = ClientConn.ClientSocket.LocalEndPoint.ToString();
            Server.PacketSent.sender = Server.ServerName;

            // Checking if the user is already in another chat room.
            foreach (UserInfo user in Server.ServerUserList)
            {
                if (Server.PacketReceived.content == user.UserName)
                {
                    if (user.CurrentChatRoom != Server.MainHall)
                    {
                        // Finishing packet.
                        Server.PacketSent.title = MsgTitle.add_user_fail.ToString();
                        Server.PacketSent.content = "Failed to add " + user.UserName + "\nUser already in another room.";
                        ClientConn.sendMsg(Server.PacketSent);
                    }
                    else
                    {
                        // Finishing packet.
                        Server.PacketSent.title = MsgTitle.add_user_success.ToString();
                        Server.PacketSent.content = "Added " + user.UserName + " to chat room";

                        // Send packet to each user in chat room, informing a new user added.
                        foreach (UserInfo u in ClientConn.ThisUser.CurrentChatRoom.UserList)
                        {
                            u.ClientConnection.sendMsg(Server.PacketSent);
                        }
                        // Update information for the added user.
                        user.CurrentChatRoom = ClientConn.ThisUser.CurrentChatRoom;
                        ClientConn.ThisUser.CurrentChatRoom.AddUser(user);
                        Server.MainHall.RemoveUser(user);
                    }
                }
            }
            if (Server.PeerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                Server.PacketSent.title = MsgTitle.client_to_chatroom.ToString();
                Server.PacketSent.content = ClientConn.ThisUser.CurrentChatRoom.RoomName + " " + Server.PacketReceived.content;

                foreach (var entry in Server.PeerServerDict.Values)
                {
                    Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(Server.PacketSent);
                }
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class CreateChatRoom : Services
    {
        public CreateChatRoom(ChatServer server, ClientConnection client) : base(server, client)
        {
            //
        }

        // Method: Create new chat room.
        public override void Serve()
        {
            // Create new local chat room, assign room name, add owner of chat room.
            string chatRoomName = "S1R" + ChatRoomCount.ToString();
            ChatRoomCount++;
            LocalChatRoom newChatRoom = new LocalChatRoom(Server, chatRoomName, ClientConn.ThisUser);

            // Update chat room list.
            Server.LocalChatRoom.Add(newChatRoom);

            // Update user's current chat room.
            ClientConn.ThisUser.CurrentChatRoom = newChatRoom;

            // Remove user from main hall.
            Server.MainHall.RemoveUser(ClientConn.ThisUser);

            Console.WriteLine("Update chat room list: " + Server.LocalChatRoom.ToString());

            // Preparing the packet to echo back to client.
            Server.PacketSent.title = MsgTitle.confirm_created.ToString();
            Server.PacketSent.time = Server.Now;
            Server.PacketSent.content = "created " + newChatRoom.RoomName + " you can start chatting now";
            Server.PacketSent.IP = ClientConn.ClientSocket.LocalEndPoint.ToString();
            Server.PacketSent.sender = Server.ServerName;

            // Sending packet.
            ClientConn.sendMsg(Server.PacketSent);

            if (Server.PeerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                Server.PacketSent.title = MsgTitle.add_chatroom.ToString();
                Server.PacketSent.content = chatRoomName;

                foreach (var entry in Server.PeerServerDict.Values)
                {
                    Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(Server.PacketSent);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class Chatting : Services
    {
        public Chatting(ChatServer server, ClientConnection client) : base(server, client)
        {
            //
        }

        // Method: Exchanging messages between clients in the same room.
        public override void Serve()
        {
            // Preparing packet to be sent.
            Server.PacketSent.time = Server.Now;
            Server.PacketSent.IP = ClientConn.ClientSocket.LocalEndPoint.ToString();
            Server.PacketSent.time = MsgTitle.chat_message.ToString();
            Server.PacketSent.sender = ClientConn.ThisUser.UserName;

            // Loop through list of client in current chat room and 
            // send message to all of them.
            foreach (UserInfo user in ClientConn.ThisUser.CurrentChatRoom.UserList)
            {
                if (user != ClientConn.ThisUser)
                {
                    Server.PacketSent.content = ClientConn.ThisUser.UserName + ": " + Server.PacketReceived.content;
                    user.ClientConnection.sendMsg(Server.PacketSent);
                }
                else
                {
                    Server.PacketSent.content = "You: " + Server.PacketReceived.content;
                    ClientConn.sendMsg(Server.PacketSent);
                }
            }
        }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class ExitRoom : Services
    {
        public ExitRoom(ChatServer server, ClientConnection client) : base(server, client)
        {
            //
        }

        // Method: Exit room.
        public override void Serve()
        {
            // Preparing packet to be sent.
            Server.PacketSent.title = MsgTitle.exit_room.ToString();
            Server.PacketSent.IP = ClientConn.ClientSocket.LocalEndPoint.ToString();
            Server.PacketSent.time = Server.Now;
            Server.PacketSent.sender = Server.ServerName;

            // Inform other users in chat room that someone is leaving.
            foreach (UserInfo user in ClientConn.ThisUser.CurrentChatRoom.UserList)
            {
                if (user != ClientConn.ThisUser)
                {
                    Server.PacketSent.content = ClientConn.ThisUser.UserName + " has left chat room";
                    user.ClientConnection.sendMsg(Server.PacketSent);
                }
                else
                {
                    Server.PacketSent.content = "Exitted chat room. You are now in " + Server.MainHall.RoomName;
                    ClientConn.sendMsg(Server.PacketSent);
                }
            }
            if (Server.PeerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                Server.PacketSent.title = MsgTitle.client_outof_chatroom.ToString();
                Server.PacketSent.content = ClientConn.ThisUser.CurrentChatRoom.RoomName + " " + ClientConn.ThisUser.UserName;

                foreach (var entry in Server.PeerServerDict.Values)
                {
                    Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(Server.PacketSent);
                }
            }
            // Remove user's current room.
            ClientConn.ThisUser.CurrentChatRoom.RemoveUser(ClientConn.ThisUser);

            // Put user in main hall.
            ClientConn.ThisUser.CurrentChatRoom = Server.MainHall;

            // Add user to main hall list of client.
            Server.MainHall.AddUser(ClientConn.ThisUser);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class JoinChatRoom : Services
    {
        public JoinChatRoom(ChatServer server, ClientConnection client) : base(server, client)
        {
            //
        }

        // Method: Add an user to a chat room when they request to join.
        public override void Serve()
        {
            // Start preparing common features of packet.
            Server.PacketSent.time = Server.Now;
            Server.PacketSent.IP = ClientConn.ClientSocket.LocalEndPoint.ToString();
            string chatRoom = null;
            bool roomAlreadyExisted = false;

            foreach (ChatRoom room in Server.LocalChatRoom)
            {
                // Check if the room requested exists.
                if (Server.PacketReceived.content.Equals(room.RoomName))
                {
                    roomAlreadyExisted = true;

                    // Remove user from main hall.
                    Server.MainHall.RemoveUser(ClientConn.ThisUser);

                    // Adding user to the room.
                    room.AddUser(ClientConn.ThisUser);

                    // Updating user's current room.
                    ClientConn.ThisUser.CurrentChatRoom = room;

                    // Preparing packet to be sent.
                    Server.PacketSent.content = "Joined " + room.RoomName + ". You can start sending message now";
                    Server.PacketSent.title = MsgTitle.confirm_joined.ToString();
                    Server.PacketSent.sender = Server.ServerName;

                    chatRoom = room.RoomName;
                    break;
                }
            }
            // If room does not exist
            if (!roomAlreadyExisted)
            {
                // Create new room and assign name.
                string roomName = "S1R" + ChatRoomCount.ToString();
                LocalChatRoom newChatRoom = new LocalChatRoom(Server, roomName, ClientConn.ThisUser);

                // Add chat room to server's list of chat room.
                Server.LocalChatRoom.Add(newChatRoom);

                // Remove user from main hall.
                Server.MainHall.RemoveUser(ClientConn.ThisUser);

                // Adding user to newly created room.
                newChatRoom.AddUser(ClientConn.ThisUser);

                // Updating user's current room.
                ClientConn.ThisUser.CurrentChatRoom = newChatRoom;

                // Preparing packet to be sent.
                Server.PacketSent.content = "Room does not exist. Created a new room: " + roomName;
                Server.PacketSent.title = MsgTitle.confirm_created.ToString();
                Server.PacketSent.sender = Server.ServerName;

                chatRoom = roomName;
            }
            ClientConn.sendMsg(Server.PacketSent);

            if (Server.PeerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                Server.PacketSent.title = MsgTitle.client_to_chatroom.ToString();
                Server.PacketSent.content = chatRoom + " " + ClientConn.ThisUser.UserName;

                foreach (var entry in Server.PeerServerDict.Values)
                {
                    Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(Server.PacketSent);
                }
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class JoinUser : Services
    {
        public JoinUser(ChatServer server, ClientConnection client) : base(server, client)
        {
            //
        }

        public override void Serve()
        {
            // "client" is the user to request to join chat room
            // that has "requestUser".
            UserInfo requestUser = null;
            foreach (UserInfo user in Server.ServerUserList)
            {
                if (user.UserName.Equals(Server.PacketReceived.content))
                {
                    requestUser = user;
                    break;
                }
            }
            if (requestUser != null)
            {
                if (requestUser.CurrentChatRoom != Server.MainHall)
                {
                    // Remove user from main hall.
                    Server.MainHall.RemoveUser(ClientConn.ThisUser);

                    // Adding user to the room.
                    requestUser.CurrentChatRoom.AddUser(ClientConn.ThisUser);

                    // Updating user's current room.
                    ClientConn.ThisUser.CurrentChatRoom = requestUser.CurrentChatRoom;

                    // Preparing packet to be sent.
                    Server.PacketSent.content = "Joined " + requestUser.CurrentChatRoom.RoomName
                        + ". You can start sending message now";
                    Server.PacketSent.title = MsgTitle.confirm_joined.ToString();
                    Server.PacketSent.sender = Server.ServerName;
                    ClientConn.sendMsg(Server.PacketSent);
                }
                else
                {
                    // Preparing packet to be sent.
                    Server.PacketSent.content = "You both are in main hall";
                    Server.PacketSent.title = MsgTitle.confirm_joined.ToString();
                    Server.PacketSent.sender = Server.ServerName;
                    ClientConn.sendMsg(Server.PacketSent);
                    Console.WriteLine("Both users are in main hall");
                }
                if (Server.PeerServerDict.Count != 0)
                {
                    // Preparing packet to send to other servers.
                    Server.PacketSent.title = MsgTitle.client_to_chatroom.ToString();
                    Server.PacketSent.content = ClientConn.ThisUser.CurrentChatRoom.RoomName
                        + " " + ClientConn.ThisUser.UserName;

                    foreach (var entry in Server.PeerServerDict.Values)
                    {
                        Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                        entry.sendMsg(Server.PacketSent);
                    }
                }
            }
            else
            {
                Console.WriteLine("Requested user " + Server.PacketReceived.content +
                    " is not connected to this server.");
                // Preparing packet to be sent.
                Server.PacketSent.content = Server.PacketReceived.content;
                Server.PacketSent.title = MsgTitle.change_server.ToString();
                Server.PacketSent.sender = Server.ServerName;
                ClientConn.sendMsg(Server.PacketSent);
                Server.FinishedChatting = true;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class KickUser : Services
    {
        public KickUser(ChatServer server, ClientConnection client) : base(server, client)
        {
            //
        }

        public override void Serve()
        {
            // The title of the received packet will be request to remove user to room.
            // The content of the received packet will be the name of the removed user.

            // Preparing packet to be sent.
            Server.PacketSent.time = Server.Now;
            Server.PacketSent.IP = ClientConn.ClientSocket.LocalEndPoint.ToString();
            Server.PacketSent.sender = Server.ServerName;

            if (Server.PeerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                Server.PacketSent.title = MsgTitle.client_outof_chatroom.ToString();
                Server.PacketSent.content = ClientConn.ThisUser.CurrentChatRoom.RoomName
                    + " " + Server.PacketReceived.content;

                foreach (var entry in Server.PeerServerDict.Values)
                {
                    Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(Server.PacketSent);
                }
            }
            // Get the object User in user list.
            foreach (UserInfo user in Server.ServerUserList)
            {
                if (Server.PacketReceived.content == user.UserName)
                {
                    // Finishing packet.
                    Server.PacketSent.title = MsgTitle.remove_user.ToString();
                    Server.PacketSent.content = "Removed " + user.UserName + " from chat room";

                    // Send packet to each user in chat room, informing a new user added.
                    foreach (UserInfo u in ClientConn.ThisUser.CurrentChatRoom.UserList)
                    {
                        if (u != user)
                        {
                            u.ClientConnection.sendMsg(Server.PacketSent);
                        }
                        else
                        {
                            Server.PacketSent.content = "You are removed from chat room";
                            u.ClientConnection.sendMsg(Server.PacketSent);
                        }
                    }
                    // Put user back to main hall, update current room info
                    user.CurrentChatRoom = Server.MainHall;
                    Server.MainHall.AddUser(user);
                    ClientConn.ThisUser.CurrentChatRoom.RemoveUser(user);
                }
            }
        }
    }

    using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
    {
        public class TerminateClient : Services
        {
            public TerminateClient(ChatServer server, ClientConnection client) : base(server, client)
            {
                //
            }

            public override void Serve()
            {
                // Preparing packet to be sent.
                Server.PacketSent.time = Server.Now;
                Server.PacketSent.IP = ClientConn.ClientSocket.LocalEndPoint.ToString();
                Server.PacketSent.title = MsgTitle.terminate_user.ToString();
                Server.PacketSent.sender = Server.ServerName;

                // Inform other user in chat room that this client is terminated.
                foreach (UserInfo user in ClientConn.ThisUser.CurrentChatRoom.UserList)
                {
                    if (user != ClientConn.ThisUser)
                    {
                        Server.PacketSent.content = ClientConn.ThisUser.UserName + " is disconnected from server";
                        user.ClientConnection.sendMsg(Server.PacketSent);
                    }
                }

                if (Server.PeerServerDict.Count != 0)
                {
                    // Preparing packet to send to other servers.
                    Server.PacketSent.title = MsgTitle.remove_client.ToString();
                    Server.PacketSent.content = ClientConn.ThisUser.UserName;

                    foreach (var entry in Server.PeerServerDict.Values)
                    {
                        Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                        entry.sendMsg(Server.PacketSent);
                    }
                }
                // Inform proxy.
                Server.PacketSent.title = MsgTitle.update_client_list.ToString();
                Server.PacketSent.content = Server.ServerUserList.Count.ToString();
                Server.ProxyConnection.sendMsg(Server.PacketSent);

                // Remove client from current chat room.
                ClientConn.ThisUser.CurrentChatRoom.RemoveUser(ClientConn.ThisUser);

                // Remove client from server's client list.
                Server.ServerUserList.Remove(ClientConn.ThisUser);

                // Remove user from server's user list.
                Server.ServerUserList.Remove(ClientConn.ThisUser);

                Server.FinishedChatting = true;
            }
        }
    }


