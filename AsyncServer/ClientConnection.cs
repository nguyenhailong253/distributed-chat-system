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
