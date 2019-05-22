/// Author : long nguyen (nguyenhailong253@gmail.com)

namespace AsyncClient
{
    /// <summary>
    ///
    /// Packet is the container of messages exchanged between
    /// servers, proxy, clients. Packet contains information 
    /// about:
    /// -  title: purpose of the request
    /// -  time: time the request is sent
    /// -  IP: local end point of the sender
    /// -  sender: name of the sender
    /// -  content: actual content of the message
    ///
    /// </summary>
    
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
