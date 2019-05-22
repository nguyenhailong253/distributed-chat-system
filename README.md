# Distributed Chat System
A simple chat system developed with C#. The program makes use of multithreading so that clients can see live messages displaying without having to wait for old message to be received/sent before sending/receiving new messages.

## Main components
- Server
Server will control a dictionary of clients’ names and IPs, a dictionary of chatrooms’ names and list of clients in that chatroom. Every time a new client connects, server will create a new thread for that client and then keep receiving new requests from new clients. In that thread server will re-direct messages from one client to another client of the same chatroom (it acts as a middle man).

- Client
Client can connect to EndPoint of the server, specify which other clients it wants to chat with. When starting client, it creates different threads for different tasks such as send, receive messages so that it can see live messages.

- Proxy
Proxy is the one monitoring servers. They know which server is online and how many clients is that server currently serving. Hence, when client request proxy will direct them to the less busy server. Proxy also handles when server suddenly dies, it will give client a new server to connect to.

## Services
- Create chat room
- Exit chat room
- Join chat room
- Join User
- Kick User
- Terminate Client
- Add User To Room
