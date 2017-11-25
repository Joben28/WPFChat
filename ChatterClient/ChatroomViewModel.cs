using SimplePackets;
using SimpleTcp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ChatterClient
{
    public class ChatroomViewModel : BaseViewModel
    {
        public ObservableCollection<ChatPacket> Messages { get; set; }
        public ObservableCollection<string> Users { get; set; }

        private string status;
        public string Status
        {
            get { return status; }
            set { OnPropertyChanged(ref status, value); }
        }

        private bool isRunning = false;
        public bool IsRunning
        {
            get { return isRunning; }
            set { OnPropertyChanged(ref isRunning, value); }
        }

        private SimpleClient client;

        private Task<bool> listenTask;
        private Task updateTask;
        private Task connectionTask;

        private DateTime pingSent;
        private DateTime pingLastSent;
        private bool pinged = false;

        public ChatroomViewModel()
        {
            Messages = new ObservableCollection<ChatPacket>();
            Users = new ObservableCollection<string>();
        }

        public async Task Connect(string username, string address, int port)
        {
            Status = "Connecting...";

            if (SetupClient(username, address, port))
            {
                var packet = await GetNewConnectionPacket(username);
                await InitializeConnection(packet);
            }
        }

        private async Task InitializeConnection(PersonalPacket connectionPacket)
        {
            pinged = false;

            if (IsRunning)
            {
                updateTask = Task.Run(() => Update());
                await client.SendObject(connectionPacket);
                connectionTask = Task.Run(() => MonitorConnection());
                Status = "Connected";
            }
            else
            {
                Status = "Connection failed";
                await Disconnect();
            }
        }

        private async Task<PersonalPacket> GetNewConnectionPacket(string username)
        {
            listenTask = Task.Run(() => client.Connect());

            IsRunning = await listenTask;

            var notifyServer = new UserConnectionPacket
            {
                Username = username,
                IsJoining = true,
                UserGuid = client.ClientId.ToString()
            };

            var personalPacket = new PersonalPacket
            {
                GuidId = client.ClientId.ToString(),
                Package = notifyServer
            };

            return personalPacket;
        }

        private bool SetupClient(string username, string address, int port)
        {
            client = new SimpleClient(address, port);
            return true;
        }

        public async Task Disconnect()
        {
            if(IsRunning)
            {
                IsRunning = false;
                await connectionTask;
                await updateTask;

                client.Disconnect();
            }

            Status = "Disconnected";

            App.Current.Dispatcher.Invoke(delegate
            {
                Messages.Add(new ChatPacket
                {
                    Username = string.Empty,
                    Message = "You have disconnected from the server.",
                    UserColor = "black"
                });
            });
        }

        public async Task Send(string username, string message, string colorCode)
        {
            ChatPacket cap = new ChatPacket
            {
                Username = username,
                Message = message,
                UserColor = colorCode
            };

            await client.SendObject(cap);
        }

        private async Task Update()
        {
            while (IsRunning)
            {
                Thread.Sleep(1);
                var recieved = await MonitorData();

                if (recieved)
                    Console.WriteLine(recieved);
            }
        }

        private async Task MonitorConnection()
        {
            pingSent = DateTime.Now;
            pingLastSent = DateTime.Now;

            while (IsRunning)
            {
                Thread.Sleep(1);
                var timePassed = (pingSent.TimeOfDay - pingLastSent.TimeOfDay);
                if (timePassed > TimeSpan.FromSeconds(5))
                {
                    if (!pinged)
                    {
                        var result = await client.PingConnection();
                        pinged = true;

                        Thread.Sleep(5000);

                        if (pinged)
                            Task.Run(() => Disconnect());
                    }
                }
                else
                {
                    pingSent = DateTime.Now;
                }
            }
        }

        private async Task<bool> MonitorData()
        {
            var newObject = await client.RecieveObject();

            App.Current.Dispatcher.Invoke(delegate
            {
                return ManagePacket(newObject);
            });

            return false;
        }

        private bool ManagePacket(object packet)
        {
            if (packet != null)
            {
                if (packet is ChatPacket chatP)
                {
                    Messages.Add(chatP);
                }

                if (packet is UserConnectionPacket connectionP)
                {
                    Users.Clear();
                    foreach (var user in connectionP.Users)
                    {
                        Users.Add(user);
                    }
                }

                if (packet is PingPacket pingP)
                {
                    pingLastSent = DateTime.Now;
                    pingSent = pingLastSent;
                    pinged = false;
                }

                return true;
            }

            return false;
        }

        public void Clear()
        {
            Messages.Clear();
            Users.Clear();
        }
    }
}
