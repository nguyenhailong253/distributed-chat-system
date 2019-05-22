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
