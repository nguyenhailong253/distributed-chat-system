// Author : long nguyen (nguyenhailong253@gmail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server2
{
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
