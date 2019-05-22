using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class JoinUser : Services
    {
        public JoinUser(ChatServer server, ClientConnection client) : base(server, client)
        {
            //
        }

        public override void Serve()
        {
            // "client" is the user to request to join chat room
            // that has "requestUser".
            UserInfo requestUser = null;
            foreach (UserInfo user in Server.ServerUserList)
            {
                if (user.UserName.Equals(Server.PacketReceived.content))
                {
                    requestUser = user;
                    break;
                }
            }
            if (requestUser != null)
            {
                if (requestUser.CurrentChatRoom != Server.MainHall)
                {
                    // Remove user from main hall.
                    Server.MainHall.RemoveUser(ClientConn.ThisUser);

                    // Adding user to the room.
                    requestUser.CurrentChatRoom.AddUser(ClientConn.ThisUser);

                    // Updating user's current room.
                    ClientConn.ThisUser.CurrentChatRoom = requestUser.CurrentChatRoom;

                    // Preparing packet to be sent.
                    Server.PacketSent.content = "Joined " + requestUser.CurrentChatRoom.RoomName
                        + ". You can start sending message now";
                    Server.PacketSent.title = MsgTitle.confirm_joined.ToString();
                    Server.PacketSent.sender = Server.ServerName;
                    ClientConn.sendMsg(Server.PacketSent);
                }
                else
                {
                    // Preparing packet to be sent.
                    Server.PacketSent.content = "You both are in main hall";
                    Server.PacketSent.title = MsgTitle.confirm_joined.ToString();
                    Server.PacketSent.sender = Server.ServerName;
                    ClientConn.sendMsg(Server.PacketSent);
                    Console.WriteLine("Both users are in main hall");
                }
                if (Server.PeerServerDict.Count != 0)
                {
                    // Preparing packet to send to other servers.
                    Server.PacketSent.title = MsgTitle.client_to_chatroom.ToString();
                    Server.PacketSent.content = ClientConn.ThisUser.CurrentChatRoom.RoomName
                        + " " + ClientConn.ThisUser.UserName;

                    foreach (var entry in Server.PeerServerDict.Values)
                    {
                        Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                        entry.sendMsg(Server.PacketSent);
                    }
                }
            }
            else
            {
                Console.WriteLine("Requested user " + Server.PacketReceived.content +
                    " is not connected to this server.");
                // Preparing packet to be sent.
                Server.PacketSent.content = Server.PacketReceived.content;
                Server.PacketSent.title = MsgTitle.change_server.ToString();
                Server.PacketSent.sender = Server.ServerName;
                ClientConn.sendMsg(Server.PacketSent);
                Server.FinishedChatting = true;
            }
        }
    }
}
