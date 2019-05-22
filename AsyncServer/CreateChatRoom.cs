using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class CreateChatRoom : Services
    {
        public CreateChatRoom(ChatServer server, ClientConnection client): base(server, client)
        {
            //
        }

        // Method: Create new chat room.
        public override void Serve()
        {
            // Create new local chat room, assign room name, add owner of chat room.
            string chatRoomName = "S1R" + ChatRoomCount.ToString();
            ChatRoomCount++;
            LocalChatRoom newChatRoom = new LocalChatRoom(Server, chatRoomName, ClientConn.ThisUser);

            // Update chat room list.
            Server.LocalChatRoom.Add(newChatRoom);

            // Update user's current chat room.
            ClientConn.ThisUser.CurrentChatRoom = newChatRoom;

            // Remove user from main hall.
            Server.MainHall.RemoveUser(ClientConn.ThisUser);

            Console.WriteLine("Update chat room list: " + Server.LocalChatRoom.ToString());

            // Preparing the packet to echo back to client.
            Server.PacketSent.title = MsgTitle.confirm_created.ToString();
            Server.PacketSent.time = Server.Now;
            Server.PacketSent.content = "created " + newChatRoom.RoomName + " you can start chatting now";
            Server.PacketSent.IP = ClientConn.ClientSocket.LocalEndPoint.ToString();
            Server.PacketSent.sender = Server.ServerName;

            // Sending packet.
            ClientConn.sendMsg(Server.PacketSent);

            if (Server.PeerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                Server.PacketSent.title = MsgTitle.add_chatroom.ToString();
                Server.PacketSent.content = chatRoomName;

                foreach (var entry in Server.PeerServerDict.Values)
                {
                    Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(Server.PacketSent);
                }
            }
        }
    }
}
