﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainVotings
{
    public class MessageEventArgs
    {
        public Message Message { get; set; }
        public string SenderHash { get; set; }

        public MessageEventArgs(Message message, string senderHash)
        {
            this.Message = message;
            this.SenderHash = senderHash;
        }

    }

}