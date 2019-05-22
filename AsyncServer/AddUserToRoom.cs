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
