using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleTcp
{
    public delegate void PacketEventHandler(object sender, PacketEvents e);
    public delegate void PersonalPacketEventHandler(object sender, PersonalPacketEvents e);

    public class SimpleServer
    {
        public IPAddress Address { get; private set; }
        public int Port { get; private set; }

        public IPEndPoint EndPoint { get; private set; }
        public Socket Socket { get; private set; }

        public bool IsRunning { get; private set; }
        public List<SimpleClient> Connections { get; private set; }

        private Task _receivingTask;

        public event PacketEventHandler OnConnectionAccepted;
        public event PacketEventHandler OnConnectionRemoved;
        public event PacketEventHandler OnPacketReceived;
        public event PacketEventHandler OnPacketSent;

        public event PersonalPacketEventHandler OnPersonalPacketSent;
        public event PersonalPacketEventHandler OnPersonalPacketReceived;

        public SimpleServer(IPAddress address, int port)
        {
            Address = address;
            Port = port;

            EndPoint = new IPEndPoint(address, port);

            Socket = new Socket(AddressFamily.InterNetwork,
                           SocketType.Stream, ProtocolType.Tcp);

            Socket.ReceiveTimeout = 5000;
            Connections = new List<SimpleClient>();
        }

        public bool Open()
        {
            Socket.Bind(EndPoint);
            Socket.Listen(10);
            return true;
        }

        public async Task<bool> Start()
        {
            _receivingTask = Task.Run(() => MonitorStreams());
            IsRunning = true;
            await Listen();
            await _receivingTask;
            Socket.Close();
            return true;
        }

        public bool Close()
        {
            IsRunning = false;
            Connections.Clear();
            return true;
        }

        public async Task<bool> Listen()
        {
            while (IsRunning)
            {
                if (Socket.Poll(100000, SelectMode.SelectRead))
                {
                    var newConnection = Socket.Accept();
                    if (newConnection != null)
                    {
                        var client = new SimpleClient();
                        var newGuid = await client.CreateGuid(newConnection);
                        await client.SendMessage(newGuid);
                        Connections.Add(client);
                        var e = BuildEvent(client, null, String.Empty);
                        OnConnectionAccepted?.Invoke(this, e);
                    }
                }
            }
            return true;
        }

        private void MonitorStreams()
        {
            while (IsRunning)
            {
                foreach(var client in Connections.ToList())
                {
                    if (!client.IsSocketConnected())
                    {
                        var e5 = BuildEvent(client, null, String.Empty);
                        Connections.Remove(client);
                        OnConnectionRemoved?.Invoke(this, e5);
                        continue;
                    }

                    if(client.Socket.Available != 0)
                    {
                        var readObject = ReadObject(client.Socket);
                        var e1 = BuildEvent(client, null, readObject);
                        OnPacketReceived?.Invoke(this, e1);

                        if(readObject is PingPacket ping)
                        {
                            client.SendObject(ping).Wait();
                            continue;
                        }

                        if(readObject is PersonalPacket pp)
                        {
                            var destination = Connections.FirstOrDefault(c => c.ClientId.ToString() == pp.GuidId);
                            var e4 = BuildEvent(client, destination, pp);
                            OnPersonalPacketReceived?.Invoke(this, e4);

                            if(destination != null)
                            {
                                destination.SendObject(pp).Wait();
                                var e2 = BuildEvent(client, destination, pp);
                                OnPersonalPacketSent?.Invoke(this, e2);
                            }
                        }
                        else
                        {
                            foreach (var c in Connections.ToList())
                            {
                                c.SendObject(readObject).Wait();
                                var e3 = BuildEvent(client, c, readObject);
                                OnPacketSent?.Invoke(this, e3);
                            }
                        }
                    }
                }
            }
        }

        public void SendObjectToClients(object package)
        {
            foreach (var c in Connections.ToList())
            {
                c.SendObject(package).Wait();
                var e3 = BuildEvent(c, c, package);
                OnPacketSent?.Invoke(this, e3);
            }
        }

        private object ReadObject(Socket clientSocket)
        {
            byte[] data = new byte[clientSocket.ReceiveBufferSize];

            using (Stream s = new NetworkStream(clientSocket))
            {
                s.Read(data, 0, data.Length);
                var memory = new MemoryStream(data);
                memory.Position = 0;

                var formatter = new BinaryFormatter();
                var obj = formatter.Deserialize(memory);

                return obj;
            }
        }

        private PacketEvents BuildEvent(SimpleClient sender, SimpleClient receiver, object package)
        {
            return new PacketEvents
            {
                Sender = sender,
                Receiver = receiver,
                Packet = package
            };
        }

        private PersonalPacketEvents BuildEvent(SimpleClient sender, SimpleClient receiver, PersonalPacket package)
        {
            return new PersonalPacketEvents
            {
                Sender = sender,
                Receiver = receiver,
                Packet = package
            };
        }
    }
}
