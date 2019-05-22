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

        public Services (ChatServer server, ClientConnection client)
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
