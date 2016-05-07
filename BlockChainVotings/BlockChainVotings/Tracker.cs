﻿using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainVotings
{
    public class Tracker
    {

        public EndPoint Address { get; }
        public Connection Connection { get; private set; }

        public int ErrorsCount { get; private set; }
        List<Tracker> allTrackers;


        public TrackerStatus Status { get; private set; }
        public int PeersRequestsCount { get; private set; }

        //события на приход сообщений для обработки в пире
        public event EventHandler<MessageEventArgs> OnDisconnectPeer;
        public event EventHandler<MessageEventArgs> OnPeerHashMessage;
        public event EventHandler<MessageEventArgs> OnRequestPeersMessage;

        //получение пиров от пира через трекер
        public event EventHandler<MessageEventArgs> OnPeersMessageFromPeer;
        //получение пиров от трекера
        public event EventHandler<MessageEventArgs> OnPeersMessageFromTracker;
        //запрос на подключение через трекер
        public event EventHandler<MessageEventArgs> OnConnectToPeerWithTrackerMessage;

        //события на приход сообщений для обработки вне
        public event EventHandler<MessageEventArgs> OnRequestBlocksMessage;
        public event EventHandler<MessageEventArgs> OnRequestTransactionsMessage;
        public event EventHandler<MessageEventArgs> OnBlocksMessage;
        public event EventHandler<MessageEventArgs> OnTransactionsMessage;

        //событие при ошибке трекера
        public event EventHandler OnTrackerError;


        public Tracker(EndPoint address, List<Tracker> allTrackers)
        {
            this.Address = address;
            this.Status = TrackerStatus.Disconnected;
            this.allTrackers = allTrackers;


            //Connect();
        }

        public void SendMessageToPeer(Message message, Peer peer)
        {
            if (Status == TrackerStatus.Connected /*&& Connection.ConnectionAlive()*/)
            {
                var shellMessage = new ToPeerMessage(CommonInfo.GetLocalEndPoint(), peer.Address, message);
                Connection.SendObject(message.GetType().Name, shellMessage);
            }
            else Connect();
        }

        public void ConnectPeerToPeer(Peer peerToConnect)
        {
            if (Status == TrackerStatus.Connected /*&& Connection.ConnectionAlive()*/)
            {
                var message = new ConnectToPeerWithTrackerMessage(CommonInfo.GetLocalEndPoint(), peerToConnect.Address);
                Connection.SendObject(message.GetType().Name, message);
            }
            else Connect();
        }


        public void Connect()
        {
            if (Status == TrackerStatus.Disconnected)
            {
                try
                {
                    ConnectionInfo connInfo = new ConnectionInfo(Address);
                    Connection newTCPConn = TCPConnection.GetConnection(connInfo);

                    Status = TrackerStatus.Connected;
                    Connection = newTCPConn;


                    Connection.AppendIncomingPacketHandler<ToPeerMessage>(typeof(ToPeerMessage).Name, OnToPeerMessage);

                    Connection.AppendIncomingPacketHandler<PeerHashMessage>(typeof(PeerHashMessage).Name, OnPeerHashMessageFromTracker);

                    Connection.AppendIncomingPacketHandler<PeersMessage>(typeof(PeersMessage).Name,
                        (p, c, m) =>
                        {
                            if (OnPeersMessageFromTracker != null)
                                OnPeersMessageFromTracker(this, new MessageEventArgs(m, null, Address));
                        });

                    Connection.AppendIncomingPacketHandler<ConnectToPeerWithTrackerMessage>(typeof(ConnectToPeerWithTrackerMessage).Name,
                        (p, c, m) =>
                        {
                            if (OnConnectToPeerWithTrackerMessage != null)
                                OnConnectToPeerWithTrackerMessage(this, new MessageEventArgs(m, null, Address));
                        });

                }
                catch (Exception ex)
                {
                    ErrorsCount++;
                    Status = TrackerStatus.Disconnected;

                    if (ErrorsCount >= 3)
                    {
                        allTrackers.Remove(this);

                        OnTrackerError(this, new EventArgs());
                    }
                }
            }
        }

        private void OnPeerHashMessageFromTracker(PacketHeader packetHeader, Connection connection, PeerHashMessage incomingObject)
        {
            var messageToSend = new PeerHashMessage(CommonInfo.LocalHash, false);
            Connection.SendObject(messageToSend.GetType().Name, messageToSend);
        }



        private void OnToPeerMessage(PacketHeader packetHeader, Connection connection, ToPeerMessage incomingObject)
        {
            if (incomingObject.RecieverAddress.Equals(CommonInfo.GetLocalEndPoint()))
            {
                var eventArgs = new MessageEventArgs(incomingObject.Message, null, incomingObject.SenderAddress);

                //при получении сообщения для пира, ивлекаем его из обертки и вызываем соотвествующее событие
                if (incomingObject.Message is PeerDisconnectMessage && OnDisconnectPeer!=null)
                {
                    OnDisconnectPeer(this, eventArgs);
                }
                else if (incomingObject.Message is PeerHashMessage && OnPeerHashMessage != null)
                {
                    OnPeerHashMessage(this, eventArgs);
                }
                else if (incomingObject.Message is RequestPeersMessage && OnRequestPeersMessage != null)
                {
                    OnRequestPeersMessage(this, eventArgs);
                }
                else if (incomingObject.Message is PeersMessage && OnPeersMessageFromPeer != null)
                {
                    OnPeersMessageFromPeer(this, eventArgs);
                }
                else if (incomingObject.Message is RequestBlocksMessage && OnRequestBlocksMessage != null)
                {
                    OnRequestBlocksMessage(this, eventArgs);
                }
                else if (incomingObject.Message is RequestTransactionsMessage && OnRequestTransactionsMessage != null)
                {
                    OnRequestTransactionsMessage(this, eventArgs);
                }
                else if (incomingObject.Message is BlocksMessage && OnBlocksMessage != null)
                {
                    OnBlocksMessage(this, eventArgs);
                }
                else if (incomingObject.Message is TransactionsMessage && OnTransactionsMessage != null)
                {
                    OnTransactionsMessage(this, eventArgs);
                }              
            }
        }

        public void Disconnect()
        {
            allTrackers.Remove(this);

            if (Status == TrackerStatus.Connected)
            {

                var message = new PeerDisconnectMessage(CommonInfo.GetLocalEndPoint());
                Connection.SendObject(message.GetType().Name, message);

            }

            if (Connection != null)
            {
                Connection.Dispose();
                Connection = null;
            }

            Status = TrackerStatus.Disconnected;
        }


        public void RequestPeersFromTracker(int count)
        {
            if (Status == TrackerStatus.Connected /*&& Connection.ConnectionAlive()*/)
            {
                var message = new RequestPeersMessage(count);
                Connection.SendObject(message.GetType().Name, message);
            }
            else Connect();
        }
    }

}
