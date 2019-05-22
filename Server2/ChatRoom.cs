using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server2
{
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
