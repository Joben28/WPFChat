using SimplePackets;
using SimpleTcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationTest
{
    class Program
    {
        private static SimpleClient client;
        private static SimpleServer server;

        static void Main(string[] args)
        {
            Console.ReadKey();
            server = new SimpleServer(IPAddress.Any, 8000);
            Task.Run(() => server.Start());

            while (!server.IsRunning) ;

            client = new SimpleClient("0.0.0.0", 8000);

            Task<bool> connection = Connect();
            connection.Wait();

            Task<bool> sending = SendObject();
            sending.Wait();

            Task<object> receiving = RecieveObject();
            receiving.Wait();

            var receive = receiving.Result;
            var packet = (PersonalPacket)receive;
            var package = (BasicPacket)packet.Package;
            Console.WriteLine(package.Heading);

            Console.ReadKey();
        }

        private static async Task<object> RecieveObject(bool secondAttempt = false)
        {
            Console.WriteLine("Receiving object...");
            var result = await client.RecieveObject();

            if(result != null)
                Console.WriteLine("Object received!");
            else if(!secondAttempt)
            {
                Console.WriteLine("Reattempting...");
                await RecieveObject(true);
            }
            else
                Console.WriteLine("Object failure");

            return result;
        }

        private static async Task<bool> SendObject()
        {
            Console.WriteLine("Sending object...");
            BasicPacket send = new BasicPacket { Heading = "TestH", Content = "TestC" };
            PersonalPacket packet = new PersonalPacket { GuidId = client.ClientId.ToString(), Package = send };
            var result = await client.SendObject(packet);
            Console.WriteLine("Object sent!" + result);
            return result;
        }

        private static async Task<bool> Connect()
        {
            Console.WriteLine("Connecting...");
            var result = await client.Connect();
            Console.WriteLine(result);

            return result;
        }
    }
}
