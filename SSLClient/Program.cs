using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSLClient
{
    class Program
    {
        static int Main(string[] args)
        {
            string serverCertificateName = "C:\\Users\\name\\public.pem";
            string machineName = "localhost";
            
            // User can specify the machine name and server name.
            // Server name must match the name on the server's certificate. 
            
            SslTcpClient.RunClient(machineName, serverCertificateName);
            Console.ReadLine();
            return 0;
        }
    }
}
