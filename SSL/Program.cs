using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SSL
{
    public class Program
    {
        /*
        static void Main(string[] args)
        {
            // The path to the certificate.
            string Certificate = "C:\\Users\\name\\test-cert.pfx";

            // Load the certificate into an X509Certificate object.
            X509Certificate cert = X509Certificate.CreateFromCertFile(Certificate);

            // Get the value.
            string resultsTrue = cert.ToString(true);

            // Display the value to the console.
            Console.WriteLine(resultsTrue);

            // Get the value.
            string resultsFalse = cert.ToString(false);

            // Display the value to the console.
            Console.WriteLine(resultsFalse);
            Console.ReadLine();
        }*/
        public static int Main(string[] args)
        {
            string certificate = "C:\\Users\\name\\test-cert.pfx";
            
            SslTcpServer.RunServer(certificate);
            Console.ReadLine();
            return 0;
        }
    }
}
