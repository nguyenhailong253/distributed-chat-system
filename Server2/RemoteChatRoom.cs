using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server2
{
    public class RemoteChatRoom : ChatRoom
    {
        public RemoteChatRoom(ChatServer server, string name, UserInfo owner)
        {
            ServerInCharge = server;
            RoomName = name;
            
            UserList.Add(owner);
            
        }

        
    }
}
