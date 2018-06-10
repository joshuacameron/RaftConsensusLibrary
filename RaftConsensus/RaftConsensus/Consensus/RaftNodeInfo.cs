﻿using System;

namespace TeamDecided.RaftConsensus.Consensus
{
    public class NodeInfo
    {
        public string NodeName { get; private set; }
        public int NextIndex { get; set; }
        public int MatchIndex { get; set; }
        public bool VoteGranted { get; set; }
        public DateTime LastReceived { get; private set; }
        public DateTime RpcDue { get; set; }

        public NodeInfo(string nodeName)
        {
            this.NodeName = nodeName;
            LastReceived = DateTime.Now;
            NextIndex = 0;
            MatchIndex = -1;
        }

        public void UpdateLastReceived()
        {
            LastReceived = DateTime.Now;
        }
    }
}