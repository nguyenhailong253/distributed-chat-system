/// Author: long nguyen (nguyenhailong253@gmail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
    /// <summary>
    /// 
    /// Containing information about server such as name,
    /// IP (local end point), status, length of current 
    /// client list, instance of client connection.
    /// 
    /// </summary>
    public class ServerInfo
    {
        private string _serverName = null;
        private string _serverIP = null;
        private string _serverStatus = "online";
        private int _userListLength = 0;
        private ClientConnection _serverConnection = null;

        public string ServerName
        {
            get { return _serverName; }
            set { _serverName = value; }
        }

        public string ServerEndPoint
        {
            get { return _serverIP; }
            set { _serverIP = value; }
        }

        public string ServerStatus
        {
            get { return _serverStatus; }
            set { _serverStatus = value; }
        }

        public int UserListLength
        {
            get { return _userListLength; }
            set { _userListLength = value; }
        }

        public ClientConnection ServerConnection
        {
            get { return _serverConnection; }
            set { _serverConnection = value; }
        }
    }
}
