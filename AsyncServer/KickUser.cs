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
}
