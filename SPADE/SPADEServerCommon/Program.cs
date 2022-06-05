using System;

namespace SPADE.ServerApp {
    internal class Program {
        public static void Main(string[] arrArgs) {
            try {
                if (arrArgs.Length < 2) {
                    Console.WriteLine("Usage: server.exe <bindIP> <port>");
                    Environment.Exit(1);
                }

                System.Net.IPEndPoint ipepBindTo = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(arrArgs[0]), int.Parse(arrArgs[1]));

                using (Server srv = new Server(ipepBindTo)) {
                    srv.FatalError += Server_FatalError;
                    srv.NonfatalError += Server_NonfatalError;
                    srv.ReceivedRequest += Server_ReceivedRequest;
                    srv.SentResponse += Server_SentResponse;
                    srv.ReceivedConfirmation += Server_ReceivedConfirmation;
                    srv.ResponseConfirmed += Server_ResponseConfirmed;

                    Console.WriteLine($"[{DateTime.Now}] Server started. Press any key to stop.");
                    Console.ReadKey(true);
                    Console.WriteLine($"[{DateTime.Now}] Stopping server.");
                }
            } catch (Exception ex) {
                Console.WriteLine($"[{DateTime.Now}] Fatal error: " + ex.ToString());
                Environment.Exit(1);
            }
        }

        private static void Server_FatalError(Server srv, Exception ex) {
            Console.WriteLine($"[{DateTime.Now}] Fatal server error: {ex}");
            Environment.Exit(1);
        }

        private static void Server_NonfatalError(Server srv, Exception ex) {
            Console.WriteLine($"[{DateTime.Now}] Error: {ex}");
        }

        private static void Server_ReceivedRequest(Server srv, System.Net.IPEndPoint ipepRemote) {
            Console.WriteLine($"[{DateTime.Now}] Received request from {ipepRemote}");
        }

        private static void Server_SentResponse(Server srv, System.Net.IPEndPoint ipepRemote) {
            Console.WriteLine($"[{DateTime.Now}] Sent response to {ipepRemote}");
        }

        private static void Server_ReceivedConfirmation(Server srv, System.Net.IPEndPoint ipepRemote) {
            Console.WriteLine($"[{DateTime.Now}] Received confirmation from {ipepRemote}");
        }

        private static void Server_ResponseConfirmed(Server srv, System.Net.IPEndPoint ipepRemote) {
            Console.WriteLine($"[{DateTime.Now}] Response to {ipepRemote} confirmed");
        }
    }
}
