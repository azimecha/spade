using System;

namespace SPADE.TestClient {
    internal class Program {
        static void Main(string[] arrArgs) {
            try {
                if (arrArgs.Length < 4) {
                    Console.WriteLine("Usage: client.exe <bindIP> <bindPort> <serverHostname> <serverPort>");
                    Environment.Exit(1);
                }

                System.Net.IPEndPoint ipepSelf = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(arrArgs[0]), int.Parse(arrArgs[1]));
                string strServerHost = arrArgs[2];
                int nServerPort = int.Parse(arrArgs[3]);

                using (Client client = new Client(ipepSelf)) {
                    client.Timeout = new TimeSpan(1, 0, 0);
                    System.Net.IPEndPoint ipepPublic = client.PerformTransaction(strServerHost, nServerPort);
                    Console.WriteLine($"Public endpoint: {ipepPublic}");
                }
            } catch (Exception ex) {
                Console.WriteLine($"Error: {ex}");
                Environment.Exit(1);
            }
        }
    }
}
