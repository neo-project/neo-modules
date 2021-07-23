using System;
using System.Collections.Generic;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class CleanupTable
    {
        public class EpochStamp
        {
            public ulong Epoch;
            public bool RemoveFlag;
        }

        private readonly object lockObject = new();
        private Dictionary<string, EpochStamp> lastAccess = new();
        private readonly ulong threshold;
        public readonly bool Enabled;

        public CleanupTable(bool enabled, ulong threshold)
        {
            this.Enabled = enabled;
            this.threshold = threshold;
        }

        public void Update(API.Netmap.NodeInfo[] snapshot, ulong now)
        {
            lock (lockObject)
            {
                var newMap = new Dictionary<string, EpochStamp>();
                foreach (var item in snapshot)
                {
                    var key = item.PublicKey.ToByteArray().ToHexString();
                    if (lastAccess.TryGetValue(key, out EpochStamp access))
                    {
                        access.RemoveFlag = false;
                        newMap[key] = access;
                    }
                    else
                    {
                        newMap[key] = new EpochStamp() { Epoch = now };
                    }
                }
                lastAccess = newMap;
            }
        }

        public bool Touch(string key, ulong now)
        {
            lock (lockObject)
            {
                EpochStamp epochStamp = null;
                bool result = false;
                if (lastAccess.TryGetValue(key, out EpochStamp access))
                {
                    epochStamp = access;
                    result = !epochStamp.RemoveFlag;
                }
                else
                {
                    epochStamp = new EpochStamp();
                }
                epochStamp.RemoveFlag = false;
                if (now > epochStamp.Epoch)
                {
                    epochStamp.Epoch = now;
                }
                lastAccess[key] = epochStamp;
                return result;
            }
        }

        public void Flag(string key)
        {
            lock (lockObject)
            {
                if (lastAccess.TryGetValue(key, out EpochStamp access))
                {
                    access.RemoveFlag = true;
                    lastAccess[key] = access;
                }
                else
                {
                    lastAccess[key] = new EpochStamp() { RemoveFlag = true };
                }
            }
        }

        public void ForEachRemoveCandidate(ulong epoch, Action<string> f)
        {
            lock (lockObject)
            {
                foreach (var item in lastAccess)
                {
                    var key = item.Key;
                    var access = item.Value;
                    if (epoch - access.Epoch > threshold)
                    {
                        access.RemoveFlag = true;
                        lastAccess[key] = access;
                        f(key);
                    }
                }
            }
        }
    }
}
