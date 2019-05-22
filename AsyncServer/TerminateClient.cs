using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class TerminateClient : Services
    {
        public TerminateClient(ChatServer server, ClientConnection client) : base(server, client)
        {
            //
        }

        public override void Serve()
        {
            // Preparing packet to be sent.
            Server.PacketSent.time = Server.Now;
            Server.PacketSent.IP = ClientConn.ClientSocket.LocalEndPoint.ToString();
            Server.PacketSent.title = MsgTitle.terminate_user.ToString();
            Server.PacketSent.sender = Server.ServerName;

            // Inform other user in chat room that this client is terminated.
            foreach (UserInfo user in ClientConn.ThisUser.CurrentChatRoom.UserList)
            {
                if (user != ClientConn.ThisUser)
                {
                    Server.PacketSent.content = ClientConn.ThisUser.UserName + " is disconnected from server";
                    user.ClientConnection.sendMsg(Server.PacketSent);
                }
            }

            if (Server.PeerServerDict.Count != 0)
            {
                // Preparing packet to send to other servers.
                Server.PacketSent.title = MsgTitle.remove_client.ToString();
                Server.PacketSent.content = ClientConn.ThisUser.UserName;

                foreach (var entry in Server.PeerServerDict.Values)
                {
                    Server.PacketSent.IP = entry.ClientSocket.LocalEndPoint.ToString();
                    entry.sendMsg(Server.PacketSent);
                }
            }
            // Inform proxy.
            Server.PacketSent.title = MsgTitle.update_client_list.ToString();
            Server.PacketSent.content = Server.ServerUserList.Count.ToString();
            Server.ProxyConnection.sendMsg(Server.PacketSent);

            // Remove client from current chat room.
            ClientConn.ThisUser.CurrentChatRoom.RemoveUser(ClientConn.ThisUser);

            // Remove client from server's client list.
            Server.ServerUserList.Remove(ClientConn.ThisUser);

            // Remove user from server's user list.
            Server.ServerUserList.Remove(ClientConn.ThisUser);

            Server.FinishedChatting = true;
        }
    }
}
