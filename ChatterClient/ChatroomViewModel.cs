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

        private string _status;
        public string Status
        {
            get { return _status; }
            set { OnPropertyChanged(ref _status, value); }
        }

        private bool _isRunning = false;
        public bool IsRunning
        {
            get { return _isRunning; }
            set { OnPropertyChanged(ref _isRunning, value); }
        }

        private SimpleClient _client;

        private Task<bool> _listenTask;
        private Task _updateTask;
        private Task _connectionTask;

        private DateTime _pingSent;
        private DateTime _pingLastSent;
        private bool _pinged = false;

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
            _pinged = false;

            if (IsRunning)
            {
                _updateTask = Task.Run(() => Update());
                await _client.SendObject(connectionPacket);
                _connectionTask = Task.Run(() => MonitorConnection());
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
            _listenTask = Task.Run(() => _client.Connect());

            IsRunning = await _listenTask;

            var notifyServer = new UserConnectionPacket
            {
                Username = username,
                IsJoining = true,
                UserGuid = _client.ClientId.ToString()
            };

            var personalPacket = new PersonalPacket
            {
                GuidId = _client.ClientId.ToString(),
                Package = notifyServer
            };

            return personalPacket;
        }

        private bool SetupClient(string username, string address, int port)
        {
            _client = new SimpleClient(address, port);
            return true;
        }

        public async Task Disconnect()
        {
            if(IsRunning)
            {
                IsRunning = false;
                await _connectionTask;
                await _updateTask;

                _client.Disconnect();
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

            await _client.SendObject(cap);
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
            _pingSent = DateTime.Now;
            _pingLastSent = DateTime.Now;

            while (IsRunning)
            {
                Thread.Sleep(1);
                var timePassed = (_pingSent.TimeOfDay - _pingLastSent.TimeOfDay);
                if (timePassed > TimeSpan.FromSeconds(5))
                {
                    if (!_pinged)
                    {
                        var result = await _client.PingConnection();
                        _pinged = true;

                        Thread.Sleep(5000);

                        if (_pinged)
                            Task.Run(() => Disconnect());
                    }
                }
                else
                {
                    _pingSent = DateTime.Now;
                }
            }
        }

        private async Task<bool> MonitorData()
        {
            var newObject = await _client.RecieveObject();

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
                    _pingLastSent = DateTime.Now;
                    _pingSent = _pingLastSent;
                    _pinged = false;
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
