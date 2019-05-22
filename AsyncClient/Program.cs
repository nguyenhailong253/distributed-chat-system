using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AsyncClient
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
