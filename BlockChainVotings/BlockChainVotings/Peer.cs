﻿using NetworkCommsDotNet.Connections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetworkCommsDotNet;
using System.Net;
using NetworkCommsDotNet.Connections.TCP;
using NetworkCommsDotNet.Connections.UDP;
using System.Reflection;

namespace BlockChainVotings
{
    public class Peer
    {
        Tracker tracker;

        //события прихода сообщений для обработки вне пира
        public event EventHandler<MessageEventArgs> OnRequestBlocksMessage;
        public event EventHandler<MessageEventArgs> OnRequestTransactionsMessage;
        public event EventHandler<MessageEventArgs> OnBlocksMessage;
        public event EventHandler<MessageEventArgs> OnTransactionsMessage;
        public event EventHandler<MessageEventArgs> OnPeersMessage;


        public EndPoint Address { get; }
        public Connection Connection { get; private set; }
        public string Hash { get; private set; }
        public int SendToOthersCount { get; private set; }
        public PeerStatus Status { get; private set; }
        public ConnectionMode ConnectionType { get; private set; }
        public int ErrorsCount { get; private set; }
        public int PeersRequestsCount { get; private set; }

        List<Peer> allPeers;
        static PeerComparer peerComparer = new PeerComparer();


        public Peer(EndPoint address, List<Peer> allPeers, Tracker tracker = null)
        {
            this.Address = address;
            this.allPeers = allPeers;
            this.tracker = tracker;
            this.Status = PeerStatus.Disconnected;

        }



        public void Connect(bool withTracker = false)
        {

            /*if (Status == PeerStatus.NoHashRecieved)
            {
                RequestPeerHash();
                return;
            }
            else*/
            if (Status == PeerStatus.Connected) return;


            //попытка подключения сразу через трекер (используется если поступил запрос от трекера)
            if (withTracker || true)
                ConnectWithTracker();
            else
            {
                //пытаемся подключиться напрямую
                try
                {
                    ConnectionInfo connInfo = new ConnectionInfo(Address);
                    TCPConnection newTCPConn = TCPConnection.GetConnection(connInfo);

                    ConnectionType = ConnectionMode.Direct;
                    Status = PeerStatus.NoHashRecieved;
                    Connection = newTCPConn;

                    //обработчики приходящих сообщений внутри пира
                    //Connection.AppendShutdownHandler((c) => DisconnectDirect(false));
                    Connection.AppendIncomingPacketHandler<PeerDisconnectMessage>(typeof(PeerDisconnectMessage).Name,
                        (p, c, m) => DisconnectDirect(false));

                    Connection.AppendIncomingPacketHandler<PeerHashMessage>(typeof(PeerHashMessage).Name,
                        (p, c, m) => OnPeerHashMessageDirect(m));

                    Connection.AppendIncomingPacketHandler<RequestPeersMessage>(typeof(RequestPeersMessage).Name,
                        (p, c, m) => OnRequestPeersMessageDirect(m));


                    //вызов внешних событий
                    Connection.AppendIncomingPacketHandler<RequestBlocksMessage>(typeof(RequestBlocksMessage).Name,
                        (p, c, m) => {
                            if (OnRequestBlocksMessage != null)
                                OnRequestBlocksMessage(this, new MessageEventArgs(m, Hash, Address));
                        });

                    Connection.AppendIncomingPacketHandler<RequestTransactionsMessage>(typeof(RequestTransactionsMessage).Name,
                        (p, c, m) => {
                            if (OnRequestTransactionsMessage != null)
                                OnRequestTransactionsMessage(this, new MessageEventArgs(m, Hash, Address));
                        });

                    Connection.AppendIncomingPacketHandler<BlocksMessage>(typeof(BlocksMessage).Name,
                        (p, c, m) => {
                            if (OnBlocksMessage != null)
                                OnBlocksMessage(this, new MessageEventArgs(m, Hash, Address));
                        });

                    Connection.AppendIncomingPacketHandler<TransactionsMessage>(typeof(TransactionsMessage).Name,
                        (p, c, m) => {
                            if (OnTransactionsMessage != null)
                                OnTransactionsMessage(this, new MessageEventArgs(m, Hash, Address));
                        });

                    Connection.AppendIncomingPacketHandler<PeersMessage>(typeof(PeersMessage).Name,
                        (p, c, m) => {
                            if (OnPeersMessage != null)
                                OnPeersMessage(this, new MessageEventArgs(m, Hash, Address));
                        });


                    RequestPeerHash();
                }
                catch (Exception ex)
                {
                    Connection = null;

                    //если трекер не задан, то ошибка
                    if (tracker == null)
                    {
                        ErrorsCount++;
                        Status = PeerStatus.Disconnected;
                    }
                    //пытаемся подключиться через трекер
                    else
                    {
                        ConnectWithTracker();
                    }
                }
            }


            //если более трех ошибок подключения, то удаляем пир из списка
            if (ErrorsCount>=3 && Status == PeerStatus.Disconnected)
            {
                allPeers.Remove(this);
            }
        }


        private void ConnectWithTracker()
        {
            try
            {
                //при удалении трекера из списка отключаем пир
                tracker.OnTrackerError += RemoveTracker;

                //подписка на сообщения с трекера
                tracker.OnDisconnectPeer += OnDisconnectPeerWithTracker;
                tracker.OnPeerHashMessage += OnPeerHashMessageWithTracker;
                tracker.OnRequestPeersMessage += OnRequestPeersMessageWithTracker;


                ConnectionType = ConnectionMode.WithTracker;
                Status = PeerStatus.NoHashRecieved;

                tracker.ConnectPeerToPeer(this);

                //RequestPeerHash();

            }
            //если не удалось через трекер, то ошибка
            catch (Exception ex2)
            {
                ErrorsCount++;
                Status = PeerStatus.Disconnected;
            }
        }

        private void RemoveTracker(object sender, EventArgs e)
        {
            tracker = null;

            if (Status == PeerStatus.Connected && ConnectionType == ConnectionMode.WithTracker)
            {
                Status = PeerStatus.Disconnected;
                ConnectionType = ConnectionMode.Direct;
            }
        }

        private void OnRequestPeersMessageWithTracker(object sender, MessageEventArgs e)
        {
            if (e.SenderHash == Hash)
            {
                var message = e.Message as RequestPeersMessage;

                var peers = allPeers.ToList();
                peers.Sort(peerComparer);

                var peersToSend = peers.Where(peer => peer.Status == PeerStatus.Connected && peer.Hash != Hash);
                peersToSend = peersToSend.Take(message.Count);
                var peersAddresses = peersToSend.Select(peer => peer.Address);
                var messageToSend = new PeersMessage(peersAddresses.ToList());

                tracker.SendMessageToPeer(message, this);

                foreach (var peer in peersToSend)
                {
                    peer.SendToOthersCount++;
                }
            }
        }

        private void OnPeerHashMessageWithTracker(object sender, MessageEventArgs e)
        {
            var message = e.Message as PeerHashMessage;

            if (e.SenderHash == Hash && message.PeerHash != string.Empty)
            {
                Hash = message.PeerHash;
                Status = PeerStatus.Connected;

                if (message.NeedResponse == true)
                {
                    var messageToSend = new PeerHashMessage(Hash, false);
                    tracker.SendMessageToPeer(message, this);
                }
            }
        }

        private void OnDisconnectPeerWithTracker(object sender, MessageEventArgs e)
        {
            var message = e.Message as PeerDisconnectMessage;

            if (message.PeerAddress.Equals(Address))
            {
                Status = PeerStatus.Disconnected;
                allPeers.Remove(this);
            }
        }



        private void OnRequestPeersMessageDirect(RequestPeersMessage m)
        {
            var peers = allPeers.ToList();
            peers.Sort(peerComparer);

            var peersToSend = peers.Where(peer => peer.Status == PeerStatus.Connected && peer.Hash != Hash);
            peersToSend = peersToSend.Take(m.Count);

            var peersAddresses = peersToSend.Select(peer => peer.Address);
            var message = new PeersMessage(peersAddresses.ToList());
            Connection.SendObject(typeof(PeersMessage).Name, message);


            foreach (var peer in peersToSend)
            {
                peer.SendToOthersCount++;
            }
        }


        public void SendMessage(Message message)
        {
            if (Status == PeerStatus.Connected)
            {
                if (ConnectionType == ConnectionMode.Direct)
                {
                    //проверка на дисконнект
                    //if (Connection.ConnectionAlive())
                    //{
                        Connection.SendObject(message.GetType().Name, message);
                    //}
                    //else
                    //{
                    //    Status = PeerStatus.Disconnected;
                    //    Connect();
                    //}
                }
                else
                {
                    tracker.SendMessageToPeer(message, this);
                }
            }
            else
            {
                Connect();
            }
        }

        public void RequestPeers(int count)
        {
            if (Status == PeerStatus.Connected)
            {
                var message = new RequestPeersMessage(count);
                if (ConnectionType == ConnectionMode.Direct)
                {
                    //проверка на дисконнект
                    //if (Connection.ConnectionAlive())
                    //{
                        Connection.SendObject(message.GetType().Name, message);
                    //}
                    //else
                    //{
                    //    Status = PeerStatus.Disconnected;
                    //    Connect();
                    //}
                }
                else
                {
                    tracker.SendMessageToPeer(message, this);
                }
                PeersRequestsCount++;
            }
            else
            {
                Connect();
            }
        }

        public void DisconnectDirect(bool sendMessage = true)
        {
            allPeers.Remove(this);

            if (ConnectionType == ConnectionMode.Direct)
            {

                if (Connection != null)
                {
                    if (sendMessage)
                    {
                        var message = new PeerDisconnectMessage(CommonInfo.GetLocalEndPoint());
                        Connection.SendObject(message.GetType().Name, message);
                    }

                    Connection.Dispose();
                    Connection = null;
                }
                
            }
            //else
            //{
            //    if (sendMessage && tracker!=null && tracker.Connection != null)
            //    {
            //        var message = new PeerDisconnectMessage(CommonInfo.GetLocalEndPoint());
            //        tracker.Connection.SendObject(message.GetType().Name, message);
            //    }

            //    if (Connection != null)
            //    {
            //        Connection.Dispose();
            //        Connection = null;
            //    }

            //}

            Status = PeerStatus.Disconnected;
        }

        private void OnPeerHashMessageDirect(PeerHashMessage message)
        {
            if (message.PeerHash != string.Empty && message.PeerHash != null)
            {
                Hash = message.PeerHash;
                Status = PeerStatus.Connected;


                //возможно стоит отключить повторную отправку сообщения, чтобы они не дублировались
                if (message.NeedResponse == true)
                    {
                    var messageToSend = new PeerHashMessage(CommonInfo.LocalHash, false);
                    Connection.SendObject(messageToSend.GetType().Name, messageToSend); 
                }
            }
        }

        private void RequestPeerHash()
        {

            if (Status == PeerStatus.NoHashRecieved)
            {
                var message = new PeerHashMessage(CommonInfo.LocalHash, true);
                if (ConnectionType == ConnectionMode.Direct)
                {
                    //проверка на дисконнект
                    //if (Connection.ConnectionAlive())
                    //{
                        Connection.SendObject(message.GetType().Name, message);
                    //}
                    //else
                    //{
                    //    Status = PeerStatus.Disconnected;
                    //    Connect();
                    //}
                }
                else
                {
                    tracker.SendMessageToPeer(message, this);
                }
            }
            else
            {
                Connect();
            }
        }

    }
}
