// Author : long nguyen (nguyenhailong253@gmail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class LocalChatRoom : ChatRoom
    {      
        public LocalChatRoom(ChatServer server, string name, UserInfo owner)
        {
            ServerInCharge = server;
            RoomName = name;

            AddUser(owner);     
        }
    }
}
