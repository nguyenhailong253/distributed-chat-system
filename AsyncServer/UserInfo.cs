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

        public UserInfo (ClientConnection connection)
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
