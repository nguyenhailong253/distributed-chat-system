/// Author: long nguyen (nguyenhailong253@gmail.com)

using System;

namespace AsyncServer
{
    /// <summary>
    /// 
    /// Main hall is where all clients without chat room belong.
    /// When client first connected to server, it will be put
    /// in to main hall
    /// 
    /// </summary>
    public class MainHall : ChatRoom
    {
        public MainHall()
        {
            RoomName = "MainHall";
        }
    }
}
