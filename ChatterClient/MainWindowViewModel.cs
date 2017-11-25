﻿using SimplePackets;
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
using System.Windows.Threading;

namespace ChatterClient
{
    public class MainWindowViewModel : ObservableObject
    {
        private string username;
        public string Username
        {
            get { return username; }
            set { OnPropertyChanged(ref username, value); }
        }

        private string address;
        public string Address
        {
            get { return address; }
            set { OnPropertyChanged(ref address, value); }
        }

        private string port = "8000";
        public string Port
        {
            get { return port; }
            set { OnPropertyChanged(ref port, value); }
        }

        private string message;
        public string Message
        {
            get { return message; }
            set { OnPropertyChanged(ref message, value); }
        }

        private string colorCode;
        public string ColorCode
        {
            get { return colorCode; }
            set { OnPropertyChanged(ref colorCode, value); }
        }

        public ICommand ConnectCommand { get; set; }
        public ICommand DisconnectCommand { get; set; }
        public ICommand SendCommand { get; set; }

        private Chatroom chatRoom;
        public Chatroom ChatRoom
        {
            get { return chatRoom; }
            set { OnPropertyChanged(ref chatRoom, value); }
        }

        public MainWindowViewModel()
        {
            ChatRoom = new Chatroom();

            ConnectCommand = new RelayCommand(Connect, CanConnect);
            DisconnectCommand = new RelayCommand(Disconnect, CanDisconnect);
            SendCommand = new RelayCommand(Send, CanSend);
        }

        private void Connect()
        {
            ChatRoom = new Chatroom();
            int socketPort = 0;
            var validPort = int.TryParse(Port, out socketPort);

            if (!validPort)
            {
                DisplayError("Please provide a valid port.");
                return;
            }

            if (String.IsNullOrWhiteSpace(Address))
            {
                DisplayError("Please provide a valid address.");
                return;
            }

            if (String.IsNullOrWhiteSpace(Username))
            {
                DisplayError("Please provide a username.");
                return;
            }

            ChatRoom.Connect(Username, Address, socketPort);
        }

        private void Disconnect()
        {
            if (ChatRoom == null)
                DisplayError("You are not connected to a server.");

            Task.Run(() => ChatRoom.Disconnect());
        }

        private void Send()
        {
            if (ChatRoom == null)
                DisplayError("You are not connected to a server.");

            ChatRoom.Send(Username, Message, ColorCode);
            Message = string.Empty;
        }

        private bool CanConnect()
        {
            if (ChatRoom.IsRunning)
                return false;

            return true;
        }

        private bool CanDisconnect()
        {
            if (ChatRoom.IsRunning)
                return true;

            return false;
        }

        private bool CanSend()
        {
            if (String.IsNullOrWhiteSpace(Message))
                return false;

            if (chatRoom.IsRunning)
                return true;

            return false;
        }

        private void DisplayError(string message)
        {
            MessageBox.Show(message, "Woah there!", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}