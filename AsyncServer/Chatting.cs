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
