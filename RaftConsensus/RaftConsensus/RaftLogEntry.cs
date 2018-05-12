﻿using System;
using System.Collections.Generic;


namespace TeamDecided.RaftConsensus
{
    public class RaftLogEntry<TKey, TValue> : IEqualityComparer<TKey>, ICloneable where TKey : ICloneable where TValue : ICloneable
    {
        public TKey Key { get; private set; }
        public TValue Value { get; private set; }
        public int Term { get; private set; }
        public int CommitIndex { get; set; }

        public RaftLogEntry(TKey key, TValue value, int term)
        {
            Key = key;
            Value = value;
            Term = term;
        }

        public bool Equals(TKey x, TKey y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(TKey obj)
        {
            return obj.GetHashCode();
        }

        public object Clone()
        {
            RaftLogEntry<TKey, TValue> clone = new RaftLogEntry<TKey, TValue>((TKey)Key.Clone(), (TValue)Value.Clone(), Term);
            clone.CommitIndex = CommitIndex;
            return clone;
        }
    }
}
