using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client2
{
    public class Program
    {
        public static ChatClient client = new ChatClient();
        public static void Main(string[] args)
        {
            client.StartClient();
        }
    }
}
