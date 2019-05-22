using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncServer
{
    public class Program
    {
        public static ChatServer server = new ChatServer();
        public static void Main(string[] args)
        {
            server.StartListening();
        }
    }
}
