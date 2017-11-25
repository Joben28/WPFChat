using SimplePackets;
using SimpleTcp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ChatterServer
{
    public class MainWindowViewModel : ObservableObject
    {
        private string externalAddress;
        public string ExternalAddress
        {
            get { return externalAddress; }
            set { OnPropertyChanged(ref externalAddress, value); }
        }

        private string port = "8000";
        public string Port
        {
            get { return port; }
            set { OnPropertyChanged(ref port, value); }
        }

        private string status = "Idle";
        public string Status
        {
            get { return status; }
            set { OnPropertyChanged(ref status, value); }
        }

        private int clientsConnected;
        public int ClientsConnected
        {
            get { return clientsConnected; }
            set { OnPropertyChanged(ref clientsConnected, value); }
        }

        public ObservableCollection<string> Outputs { get; set; }
        public Dictionary<string,string> Usernames = new Dictionary<string, string>();

        public ICommand RunCommand { get; set; }
        public ICommand StopCommand { get; set; }

        private SimpleServer server;
        private bool isRunning;

        private Task runTask;
        private Task updateTask;
        private Task listenTask;

        public MainWindowViewModel()
        {
            Outputs = new ObservableCollection<string>();

            RunCommand = new RelayCommand(Run);
            StopCommand = new RelayCommand(Stop);
        }

        private void Run()
        {
            runTask = Task.Factory.StartNew(() =>
            {
                Status = "Connecting...";
                SetupServer();
                listenTask = Task.Run(() => server.Start());
                updateTask = Task.Run(() => Update());
                isRunning = true;
            });
        }

        private void SetupServer()
        {
            Status = "Validating socket...";
            int socketPort = 0;
            var isValidPort = int.TryParse(Port, out socketPort);

            if (!isValidPort)
            {
                DisplayError("Port value is not valid.");
                return;
            }

            Status = "Obtaining IP...";
            GetExternalIp();
            Status = "Setting up server...";
            server = new SimpleServer(IPAddress.Any, socketPort);

            Status = "Setting up events...";
            server.OnConnectionAccepted += Server_OnConnectionAccepted;
            server.OnConnectionRemoved += Server_OnConnectionRemoved;
            server.OnPacketSent += Server_OnPacketSent;
            server.OnPersonalPacketSent += Server_OnPersonalPacketSent;
            server.OnPersonalPacketReceived += Server_OnPersonalPacketReceived;
            server.OnPacketReceived += Server_OnPacketReceived;
        }

        private void Update()
        {
            while(isRunning)
            {
                Thread.Sleep(5);
                if (!server.IsRunning)
                {
                    Stop();
                    return;
                }

                ClientsConnected = server.Connections.Count;
                Status = "Running";
            }
        }

        private void Stop()
        {
            ExternalAddress = string.Empty;
            isRunning = false;
            ClientsConnected = 0;
            Thread.Sleep(1000);
            Status = "Stopped";
            server.Stop();
        }


        private void GetExternalIp()
        {
            try
            {
                string externalIP;
                externalIP = (new WebClient()).DownloadString("http://checkip.dyndns.org/");
                externalIP = (new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}"))
                             .Matches(externalIP)[0].ToString();
                ExternalAddress = externalIP;
            }
            catch { ExternalAddress = "Error receiving IP address."; }
        }

        private void DisplayError(string message)
        {
            MessageBox.Show(message, "Woah there!", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Server_OnPacketSent(object sender, PacketEvents e)
        {

        }

        private void Server_OnPacketReceived(object sender, PacketEvents e)
        {

        }

        private void Server_OnPersonalPacketSent(object sender, PersonalPacketEvents e)
        {
            WriteOutput("Personal Packet Sent");
        }
        private void Server_OnPersonalPacketReceived(object sender, PersonalPacketEvents e)
        {
            if(e.Packet.Package is UserConnectionPacket ucp)
            {
                var notification = new ChatPacket
                {
                    Username = "Server",
                    Message = "A new user has joined the chat",
                    UserColor = Colors.Purple.ToString()
                };

                if (Usernames.Keys.Contains(ucp.UserGuid))
                    Usernames.Remove(ucp.UserGuid);
                else
                    Usernames.Add(ucp.UserGuid, ucp.Username);

                ucp.Users = Usernames.Values.ToArray();

                Task.Run(() => server.SendObjectToClients(ucp)).Wait();
                Thread.Sleep(500);
                Task.Run(() => server.SendObjectToClients(notification)).Wait();
            }
            WriteOutput("Personal Packet Received");
        }

        private void Server_OnConnectionAccepted(object sender, PacketEvents e)
        {
            WriteOutput("Client Connected: " + e.Sender.Socket.RemoteEndPoint.ToString());
        }

        private void Server_OnConnectionRemoved(object sender, PacketEvents e)
        {
            if(!Usernames.ContainsKey(e.Sender.ClientId.ToString()))
            {
                return;
            }

            var notification = new ChatPacket
            {
                Username = "Server",
                Message = "A user has left the chat",
                UserColor = Colors.Purple.ToString()
            };

            var userPacket = new UserConnectionPacket
            {
                UserGuid = e.Sender.ClientId.ToString(),
                Username = Usernames[e.Sender.ClientId.ToString()],
                IsJoining = false
            };

            if(Usernames.Keys.Contains(userPacket.UserGuid))
                Usernames.Remove(userPacket.UserGuid);

            userPacket.Users = Usernames.Values.ToArray();

            if(server.Connections.Count != 0)
            {
                Task.Run(() => server.SendObjectToClients(userPacket)).Wait();
                Task.Run(() => server.SendObjectToClients(notification)).Wait();
            }
            WriteOutput("Client Disconnected: " + e.Sender.Socket.RemoteEndPoint.ToString());
        }

        private void WriteOutput(string message)
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                Outputs.Add(message);
            });
        }
    }
}
