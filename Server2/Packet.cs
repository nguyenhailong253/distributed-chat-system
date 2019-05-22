// Author : long nguyen (nguyenhailong253@gmail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server2
{
    public enum MsgTitle
    {
        // client to server
        new_chatroom,
        new_user,
        terminate_user,
        join_chatroom,
        confirm_created,
        confirm_joined,
        add_user,
        chat_message,
        exit_room,
        add_user_fail,
        add_user_success,
        remove_user,
        chat_with_user,
        // server to server
        add_client,
        remove_client,
        add_chatroom,
        remove_chatroom,
        client_to_chatroom, // content = ["clientname", "roomname"]
        client_outof_chatroom,
        server_on,
        server_off,
        // server to proxy
        are_you_online,
        online,
        update_client_list,
        // client to proxy
        connect_to_server,
        server_info,
        change_server,

    };

    public class Packet
    {
        public string sender { get; set; }
        public string title { get; set; }
        public string IP { get; set; }
        public string content { get; set; }
        public string time { get; set; }
    }
}
