﻿// Author : long nguyen (nguyenhailong253@gmail.com)

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Server2
{
    // ==========<summary>============
    //
    // An asynchronous server which handles clients' requests asynchronously 
    // using multithreading. Usually using 1 separated thread to handle client request
    // 
    // Client thread will be closed when client specifies "terminate" command
    // or is suddenly disconnected.
    //
    // What server can do includes:
    // - Create chat room.
    // - Allow user to join existing chat room.
    // - User 1 request to add other user to chat room.
    // - Exit chat room.
    // - Terminate user.
    // - Allow users in same chat room to exchange messages.
    //
    // Servers in this network can talk to each other. Each peer server connection
    // will be maintained on a separated thread. Sever thread only closed when
    // one of the servers specify command "server off" or suddenly disconnected.
    //
    // All servers directly report to a Proxy about their status and their 
    // client lists.
    //
    // =================================

    // ============<rules>==============
    //
    // One client can only be in 1 chat room at a time.
    // There is no room admin.
    // Chat room is NOT eliminated when there are no users in it.
    // Client with no CurrentRoom property will be in MainHall.
    //
    // ==================================

    // ==========<convention>============
    //
    // Server name will start with "S" followed by a number.
    // eg: S1, S2.
    // Client name will start with server name followed by "C" and a number.
    // eg: S1C1, S2C3.
    // Room name will start with server name followed by "R" and a number.
    // eg: S1R2, S3R4.
    //
    // ==================================

    public class ChatServer
    {
        /*
         *  ============= ATTRIBUTES ============
         */

        // Server knows its list of users
        // and list of current chat room available.
        private List<UserInfo> _userList = new List<UserInfo>();
        private List<ChatRoom> _localCR = new List<ChatRoom>();

        // Server knows chat rooms of other servers in network
        // and list of remote users.
        // Server knows chat rooms of other servers in network
        // and list of remote users.
        // Key = server name, list = list of chat rooms/users
        private Dictionary<string, List<string>> _peerChatrooms = new Dictionary<string, List<string>>();
        private Dictionary<string, List<string>> _peerUsers = new Dictionary<string, List<string>>();
        // Key = chat room name, list = list of client in chat room
        private Dictionary<string, List<string>> _peerClientInCR = new Dictionary<string, List<string>>();

        // Key = name of server, client connection = connection with that server.
        // This will not include THIS server. Hence, total servers will be length of this dict + 1
        private Dictionary<string, ClientConnection> _peerServerDict = new Dictionary<string, ClientConnection>();
        //private List<string> _peerChatRoomList = new List<string>();
        //private List<string> _peerClientList = new List<string>();

        // Main hall with all users without current chat room.
        private MainHall _mainHall = new MainHall();

        // Main buffer of server for 
        // containing sent or received data.
        //private byte[] _mainBuffer = new byte[1024];

        // Data received in string type.
        private string dataReceived = null;

        // Server has 2 kinds of packets: send and receive.
        private Packet _packetSent = new Packet();
        private Packet _packetReceived = new Packet();

        // Number of clients/chat rooms
        private int _clientCount = 0;
        private int _chatRoomCount = 0;

        // Communicator between threads.
        private AutoResetEvent _mainThread = new AutoResetEvent(false);
        private AutoResetEvent _receiveThread = new AutoResetEvent(false);

        // Setting current time.
        private string _now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        // Variable controlling whether chat finished.
        private bool _finishedChatting = false;

        // ClientConnection of server for communication between servers.
        //private ClientConnection _serverConnection = null;

        // ClientConnection of communication between proxy and this server.
        private ClientConnection _proxyConnection = null;

        // Remote endpoint of other servers/proxy.
        private IPHostEntry _ipHostInfo = null;
        private IPAddress _ipAddress = null;
        private IPEndPoint _proxyEndPoint = null;

        // Name of server.
        private string _name = "S2";

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

        // Read-only property to access server's user list.
        public List<UserInfo> ServerUserList
        {
            get { return _userList; }
        }

        /*
         * =============== METHODS ===============
         */

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
                //Console.WriteLine("ip: " + ip + "port: " + port);
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

        // Method: Create new chat room.
        public void CreateChatRoom(ClientConnection client)
        {
            // Create new local chat room, assign room name, add owner of chat room.
            string chatRoomName = "S2R" + _chatRoomCount.ToString();
            _chatRoomCount++;
            LocalChatRoom newChatRoom = new LocalChatRoom(this, chatRoomName, client.ThisUser);

            // Update chat room list.
            _localCR.Add(newChatRoom);

            // Update user's current chat room.
            client.ThisUser.CurrentChatRoom = newChatRoom;

            // Remove user from main hall.
            _mainHall.RemoveUser(client.ThisUser);

            Console.WriteLine("Update chat room list: " + _localCR.ToString());

            // Preparing the packet to echo back to client.
            _packetSent.title = MsgTitle.confirm_created.ToString();
            _packetSent.time = _now;
            _packetSent.content = "created " + newChatRoom.RoomName + " you can start chatting now";
            _packetSent.IP = client.ClientSocket.LocalEndPoint.ToString();
            _packetSent.sender = this.ServerName;

            // Sending packet.
            client.sendMsg(_packetSent);

            if (_peerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                _packetSent.title = MsgTitle.add_chatroom.ToString();
                _packetSent.content = chatRoomName;

                foreach (var entry in _peerServerDict.Values)
                {
                    _packetSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(_packetSent);
                }
            }

        }

        // Method: Add an user to a chat room when they request to join.
        public void JoinChatroom(ClientConnection client)
        {
            // Start preparing common features of packet.
            _packetSent.time = _now;
            _packetSent.IP = client.ClientSocket.LocalEndPoint.ToString();
            string chatRoom = null;
            bool roomAlreadyExisted = false;

            foreach (ChatRoom room in _localCR)
            {
                // Check if the room requested exists.
                if (_packetReceived.content.Equals(room.RoomName))
                {
                    roomAlreadyExisted = true;

                    // Remove user from main hall.
                    _mainHall.RemoveUser(client.ThisUser);

                    // Adding user to the room.
                    room.AddUser(client.ThisUser);

                    // Updating user's current room.
                    client.ThisUser.CurrentChatRoom = room;

                    // Preparing packet to be sent.
                    _packetSent.content = "Joined " + room.RoomName + ". You can start sending message now";
                    _packetSent.title = MsgTitle.confirm_joined.ToString();
                    _packetSent.sender = this.ServerName;

                    chatRoom = room.RoomName;
                    break;
                }
            }
            // If room does not exist
            if (!roomAlreadyExisted)
            {
                // Create new room and assign name.
                string roomName = "S2R" + _chatRoomCount.ToString();
                LocalChatRoom newChatRoom = new LocalChatRoom(this, roomName, client.ThisUser);

                // Add chat room to server's list of chat room.
                _localCR.Add(newChatRoom);

                // Remove user from main hall.
                _mainHall.RemoveUser(client.ThisUser);

                // Adding user to newly created room.
                newChatRoom.AddUser(client.ThisUser);

                // Updating user's current room.
                client.ThisUser.CurrentChatRoom = newChatRoom;

                // Preparing packet to be sent.
                _packetSent.content = "Room does not exist. Created a new room: " + roomName;
                _packetSent.title = MsgTitle.confirm_created.ToString();
                _packetSent.sender = this.ServerName;

                chatRoom = roomName;
            }
            client.sendMsg(_packetSent);

            if (_peerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                _packetSent.title = MsgTitle.client_to_chatroom.ToString();
                _packetSent.content = chatRoom + " " + client.ThisUser.UserName;

                foreach (var entry in _peerServerDict.Values)
                {
                    _packetSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(_packetSent);
                }
            }
        }

        // Method: Exchanging messages between clients in the same room.
        public void Chatting(ClientConnection client)
        {
            // Preparing packet to be sent.
            _packetSent.time = _now;
            _packetSent.IP = client.ClientSocket.LocalEndPoint.ToString();
            _packetSent.time = MsgTitle.chat_message.ToString();
            _packetSent.sender = client.ThisUser.UserName;

            // Loop through list of client in current chat room and 
            // send message to all of them.
            foreach (UserInfo user in client.ThisUser.CurrentChatRoom.UserList)
            {
                if (user != client.ThisUser)
                {
                    _packetSent.content = client.ThisUser.UserName + ": " + _packetReceived.content;
                    user.ClientConnection.sendMsg(_packetSent);
                }
                else
                {
                    _packetSent.content = "You: " + _packetReceived.content;
                    client.sendMsg(_packetSent);
                }
            }
        }

        // Method: Exit room.
        public void ExitRoom(ClientConnection client)
        {
            // Preparing packet to be sent.
            _packetSent.title = MsgTitle.exit_room.ToString();
            _packetSent.IP = client.ClientSocket.LocalEndPoint.ToString();
            _packetSent.time = _now;
            _packetSent.sender = this.ServerName;

            if (_peerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                _packetSent.title = MsgTitle.client_outof_chatroom.ToString();
                _packetSent.content = client.ThisUser.CurrentChatRoom.RoomName + " " + client.ThisUser.UserName;

                foreach (var entry in _peerServerDict.Values)
                {
                    _packetSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(_packetSent);
                }
            }

            // Inform other users in chat room that someone is leaving.
            foreach (UserInfo user in client.ThisUser.CurrentChatRoom.UserList)
            {
                if (user != client.ThisUser)
                {
                    _packetSent.content = client.ThisUser.UserName + " has left chat room";
                    user.ClientConnection.sendMsg(_packetSent);
                }
                else
                {
                    _packetSent.content = "Exitted chat room. You are now in " + _mainHall.RoomName;
                    client.sendMsg(_packetSent);
                }
            }

            // Remove user's current room.
            client.ThisUser.CurrentChatRoom.RemoveUser(client.ThisUser);

            // Put user in main hall.
            client.ThisUser.CurrentChatRoom = _mainHall;

            // Add user to main hall list of client.
            _mainHall.AddUser(client.ThisUser);

            
        }

        // Method: Terminate client.
        public void TerminateClient(ClientConnection client)
        {
            // Preparing packet to be sent.
            _packetSent.time = _now;
            _packetSent.IP = client.ClientSocket.LocalEndPoint.ToString();
            _packetSent.title = MsgTitle.terminate_user.ToString();
            _packetSent.sender = this.ServerName;

            if (_peerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                _packetSent.title = MsgTitle.remove_client.ToString();
                _packetSent.content = client.ThisUser.UserName;

                foreach (var entry in _peerServerDict.Values)
                {
                    _packetSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(_packetSent);
                }
            }

            // Inform other user in chat room that this client is terminated.
            foreach (UserInfo user in client.ThisUser.CurrentChatRoom.UserList)
            {
                if (user != client.ThisUser)
                {
                    _packetSent.content = client.ThisUser.UserName + " is disconnected from server";
                    user.ClientConnection.sendMsg(_packetSent);
                }
            }

            // Remove client from current chat room.
            client.ThisUser.CurrentChatRoom.RemoveUser(client.ThisUser);

            // Remove client from server's client list.
            _userList.Remove(client.ThisUser);

            // Remove user from server's user list.
            _userList.Remove(client.ThisUser);

            _finishedChatting = true;

            
        }

        // Method: Request to add other user to room.
        public void AddUserToRoom(ClientConnection client)
        {
            // The title of the received packet will be request to add user to room.
            // The content of the received packet will be the name of the added user.

            // Preparing packet to be sent.
            _packetSent.time = _now;
            _packetSent.IP = client.ClientSocket.LocalEndPoint.ToString();
            _packetSent.sender = this.ServerName;

            // Checking if the user is already in another chat room.
            foreach (UserInfo user in _userList)
            {
                if (_packetReceived.content == user.UserName)
                {
                    if (user.CurrentChatRoom != _mainHall)
                    {
                        // Finishing packet.
                        _packetSent.title = MsgTitle.add_user_fail.ToString();
                        _packetSent.content = "Failed to add " + user.UserName + "\nUser already in another room.";
                        client.sendMsg(_packetSent);
                    }
                    else
                    {
                        // Finishing packet.
                        _packetSent.title = MsgTitle.add_user_success.ToString();
                        _packetSent.content = "Added " + user.UserName + " to chat room";

                        // Send packet to each user in chat room, informing a new user added.
                        foreach (UserInfo u in client.ThisUser.CurrentChatRoom.UserList)
                        {
                            u.ClientConnection.sendMsg(_packetSent);
                        }

                        // Update information for the added user.
                        user.CurrentChatRoom = client.ThisUser.CurrentChatRoom;
                        client.ThisUser.CurrentChatRoom.AddUser(user);
                        _mainHall.RemoveUser(user);
                    }
                }
            }

            if (_peerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                _packetSent.title = MsgTitle.client_to_chatroom.ToString();
                _packetSent.content = client.ThisUser.CurrentChatRoom.RoomName + " " + _packetReceived.content;

                foreach (var entry in _peerServerDict.Values)
                {
                    _packetSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(_packetSent);
                }
            }
        }

        // Method: Request to kick user out of room.
        public void KickUser(ClientConnection client)
        {
            // The title of the received packet will be request to remove user to room.
            // The content of the received packet will be the name of the removed user.

            // Preparing packet to be sent.
            _packetSent.time = _now;
            _packetSent.IP = client.ClientSocket.LocalEndPoint.ToString();
            _packetSent.sender = this.ServerName;

            if (_peerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                _packetSent.title = MsgTitle.client_outof_chatroom.ToString();
                _packetSent.content = client.ThisUser.CurrentChatRoom.RoomName + " " + _packetReceived.content;

                foreach (var entry in _peerServerDict.Values)
                {
                    _packetSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(_packetSent);
                }
            }

            // Get the object User in user list.
            foreach (UserInfo user in _userList)
            {
                if (_packetReceived.content == user.UserName)
                {
                    // Finishing packet.
                    _packetSent.title = MsgTitle.remove_user.ToString();
                    _packetSent.content = "Removed " + user.UserName + " from chat room";

                    // Send packet to each user in chat room, informing a new user added.
                    foreach (UserInfo u in client.ThisUser.CurrentChatRoom.UserList)
                    {
                        if (u != user)
                        {
                            u.ClientConnection.sendMsg(_packetSent);
                        }
                        else
                        {
                            _packetSent.content = "You are removed from chat room";
                            u.ClientConnection.sendMsg(_packetSent);
                        }
                    }
                    // Put user back to main hall, update current room info
                    user.CurrentChatRoom = _mainHall;
                    _mainHall.AddUser(user);
                    client.ThisUser.CurrentChatRoom.RemoveUser(user);
                }
            }
        }
        private void JoinUser(ClientConnection client)
        {
            // "client" is the user to request to join chat room
            // that has "requestUser".
            UserInfo requestUser = null;
            foreach (UserInfo user in _userList)
            {
                if (user.UserName.Equals(_packetReceived.content))
                {
                    requestUser = user;
                    break;
                }
            }
            // If the requestUser is connected to this server,
            if (requestUser != null)
            {
                if (requestUser.CurrentChatRoom != _mainHall)
                {
                    // Remove user from main hall.
                    _mainHall.RemoveUser(client.ThisUser);

                    // Adding user to the room.
                    requestUser.CurrentChatRoom.AddUser(client.ThisUser);

                    // Updating user's current room.
                    client.ThisUser.CurrentChatRoom = requestUser.CurrentChatRoom;

                    // Preparing packet to be sent.
                    _packetSent.content = "Joined " + requestUser.CurrentChatRoom.RoomName
                        + ". You can start sending message now";
                    _packetSent.title = MsgTitle.confirm_joined.ToString();
                    _packetSent.sender = this.ServerName;
                    client.sendMsg(_packetSent);
                }
                else
                {
                    // Preparing packet to be sent.
                    _packetSent.content = "You both are in main hall";
                    _packetSent.title = MsgTitle.confirm_joined.ToString();
                    _packetSent.sender = this.ServerName;
                    client.sendMsg(_packetSent);
                    Console.WriteLine("Both users are in main hall");
                }
                if (_peerServerDict.Count != 0)
                {
                    // Preparing packet to send to other servers.
                    _packetSent.title = MsgTitle.client_to_chatroom.ToString();
                    _packetSent.content = client.ThisUser.CurrentChatRoom.RoomName
                        + " " + client.ThisUser.UserName;

                    foreach (var entry in _peerServerDict.Values)
                    {
                        _packetSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                        entry.sendMsg(_packetSent);
                    }
                }
            }
            // If the requestUser is not connected to server, ask
            // client to forward the message to proxy and 
            // proxy will give a new server to client.
            else
            {
                Console.WriteLine("Requested user " + _packetReceived.content +
                    " is not connected to this server.");
                // Preparing packet to be sent.
                _packetSent.content = _packetReceived.content;
                _packetSent.title = MsgTitle.change_server.ToString();
                _packetSent.sender = this.ServerName;
                client.sendMsg(_packetSent);
                _finishedChatting = true;
            }
        }

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
                    //string sender = _packetReceived.sender;

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
                        //Console.WriteLine("sender is " + _packetReceived.sender);

                        // New socket to connect with proxy.
                        //Console.WriteLine("receive from proxy");
                        //Console.WriteLine(_packetReceived.IP);
                        Socket serverSocket = new Socket(_ipAddress.AddressFamily,
                            SocketType.Stream, ProtocolType.Tcp);
                        IPEndPoint remoteEP = createIPEndPoint(_packetReceived.IP);
                        serverSocket.Connect(remoteEP);
                        ClientConnection newServerConn = new ClientConnection(serverSocket);
                        UserInfo serverInfo = new UserInfo(newServerConn);
                        //Console.WriteLine("connect success");
                        newServerConn.GetUser(serverInfo);
                        newServerConn.Name = _packetReceived.content;

                        // Add this server to server dict.
                        //Console.WriteLine("content " + _packetReceived.content);
                        _peerServerDict[_packetReceived.content] = newServerConn;

                        // Make a new thread to maintain servers connection.
                        Thread serverThread = new Thread(ServerCommunicate);
                        serverThread.Name = newServerConn.Name;
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
                    //Console.WriteLine("start receiving...");
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
                        break; // TerminateServer()?
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
                //Console.WriteLine("serverdict count = " + _peerServerDict.Count);
                //Console.WriteLine("msg title = " + _packetReceived.title);
                if ((_peerServerDict.Count != 0) && (_packetReceived.title != MsgTitle.server_on.ToString()))
                {
                    _packetSent.title = MsgTitle.add_client.ToString();
                    _packetSent.content = client.ThisUser.UserName;
                    _packetSent.IP = client.ClientSocket.LocalEndPoint.ToString();
                    _packetSent.time = _now;
                    _packetSent.sender = this.ServerName;

                    // Inform other servers.
                    foreach (var entry in _peerServerDict.Values)
                    {
                        entry.sendMsg(_packetSent);
                        Console.WriteLine("Infomr update client to " + entry.Name);
                    }

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
                        //Console.WriteLine("Got a request from " + client.Name);
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
                        CreateChatRoom(client);
                    }
                    // or join chat room,
                    else if (_packetReceived.title.Equals(MsgTitle.join_chatroom.ToString()))
                    {
                        JoinChatroom(client);
                    }
                    // or request to add other user to chat room
                    else if (_packetReceived.title.Equals(MsgTitle.add_user.ToString()))
                    {
                        AddUserToRoom(client);
                    }
                    // or client want to terminate connection with server,
                    else if (_packetReceived.title.Equals(MsgTitle.terminate_user.ToString()))
                    {
                        TerminateClient(client);
                    }
                    // or client want to exit current chat room,
                    else if (_packetReceived.title.Equals(MsgTitle.exit_room.ToString()))
                    {
                        ExitRoom(client);
                    }
                    // or one user want to remove other user from chat room,
                    else if (_packetReceived.title.Equals(MsgTitle.remove_user.ToString()))
                    {
                        KickUser(client);
                    }
                    // or user request to chat with other user.
                    else if (_packetReceived.title.Equals(MsgTitle.chat_with_user.ToString()))
                    {
                        JoinUser(client);
                    }
                    // or just simply chatting.
                    else
                    {
                        Chatting(client);
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

        // Asynchronous method: server listening to client request.
        public void StartListening()
        {
            // Establish the local endpoint for the socket
            // using the DNS name of the local computer.  
            _ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            _ipAddress = _ipHostInfo.AddressList[0];
            _localEndPoint = new IPEndPoint(_ipAddress, 8080);

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
                    newUser.UserName = "S2C" + _clientCount.ToString();
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
