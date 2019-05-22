using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class JoinChatRoom : Services
    {
        public JoinChatRoom(ChatServer server, ClientConnection client) : base(server, client)
        {
            //
        }

        // Method: Add an user to a chat room when they request to join.
        public override void Serve()
        {
            // Start preparing common features of packet.
            Server.PacketSent.time = Server.Now;
            Server.PacketSent.IP = ClientConn.ClientSocket.LocalEndPoint.ToString();
            string chatRoom = null;
            bool roomAlreadyExisted = false;

            foreach (ChatRoom room in Server.LocalChatRoom)
            {
                // Check if the room requested exists.
                if (Server.PacketReceived.content.Equals(room.RoomName))
                {
                    roomAlreadyExisted = true;

                    // Remove user from main hall.
                    Server.MainHall.RemoveUser(ClientConn.ThisUser);

                    // Adding user to the room.
                    room.AddUser(ClientConn.ThisUser);

                    // Updating user's current room.
                    ClientConn.ThisUser.CurrentChatRoom = room;

                    // Preparing packet to be sent.
                    Server.PacketSent.content = "Joined " + room.RoomName + ". You can start sending message now";
                    Server.PacketSent.title = MsgTitle.confirm_joined.ToString();
                    Server.PacketSent.sender = Server.ServerName;

                    chatRoom = room.RoomName;
                    break;
                }
            }
            // If room does not exist
            if (!roomAlreadyExisted)
            {
                // Create new room and assign name.
                string roomName = "S1R" + ChatRoomCount.ToString();
                LocalChatRoom newChatRoom = new LocalChatRoom(Server, roomName, ClientConn.ThisUser);

                // Add chat room to server's list of chat room.
                Server.LocalChatRoom.Add(newChatRoom);

                // Remove user from main hall.
                Server.MainHall.RemoveUser(ClientConn.ThisUser);

                // Adding user to newly created room.
                newChatRoom.AddUser(ClientConn.ThisUser);

                // Updating user's current room.
                ClientConn.ThisUser.CurrentChatRoom = newChatRoom;

                // Preparing packet to be sent.
                Server.PacketSent.content = "Room does not exist. Created a new room: " + roomName;
                Server.PacketSent.title = MsgTitle.confirm_created.ToString();
                Server.PacketSent.sender = Server.ServerName;

                chatRoom = roomName;
            }
            ClientConn.sendMsg(Server.PacketSent);

            if (Server.PeerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                Server.PacketSent.title = MsgTitle.client_to_chatroom.ToString();
                Server.PacketSent.content = chatRoom + " " + ClientConn.ThisUser.UserName;

                foreach (var entry in Server.PeerServerDict.Values)
                {
                    Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(Server.PacketSent);
                }
            }
        }

    }
}
