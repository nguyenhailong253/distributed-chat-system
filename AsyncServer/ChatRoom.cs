/// Author : long nguyen (nguyenhailong253@gmail.com)

using System.Collections.Generic;

namespace AsyncServer
{
    /// <summary>
    ///
    /// Chat room is where users exchange message.
    /// It contains list of users in chat room, 
    /// which server it belongs to and its name.
    ///
    /// </summary>
    public abstract class ChatRoom
    {
        private string _chatroomName = null;
        private List<UserInfo> _userList = new List<UserInfo>();
        private ChatServer _server = null;

        public ChatServer ServerInCharge
        {
            get { return _server; }
            set { _server = value; }
        }

        public string RoomName 
        {
            get { return _chatroomName; }
            set { _chatroomName = value; }
        }

        public List<UserInfo> UserList
        {
            get { return _userList; }
            set { _userList = value; }
        }
        
        public void AddUser(UserInfo user)
        {
            _userList.Add(user);
        }

        public void RemoveUser(UserInfo user)
        {
            _userList.Remove(user);
        }

        public bool UserInRoom(UserInfo user)
        {
            if (UserList.Contains(user))
                return true;
            return false;
        }
    }
}
