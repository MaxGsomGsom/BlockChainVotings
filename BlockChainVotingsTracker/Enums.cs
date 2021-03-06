﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainVotingsTracker
{
    public enum PeerStatus
    {
        Disconnected,
        NoHashRecieved,
        Connected,
    }

    public enum MessageType
    {
        None,
        RequestPeers,
        Peers,
        RequestBlocks,
        Blocks,
        RequestTransactions,
        Transactions,
        ConnectToPeerWithTracker,
        MessageToPeer,
        PeerDisconnect,
        PeerHash
    }

    public enum TrackerStatus
    {
        Stopped,
        Started
    }

    public enum TransactionType
    {
        None = 0,
        CreateUser,
        BanUser,
        Vote,
        StartVoting,
        ChangeRootUser
    }

    public enum TransactionStatus
    {
        Free,
        InBlock,
        InPendingBlock
    }
}
