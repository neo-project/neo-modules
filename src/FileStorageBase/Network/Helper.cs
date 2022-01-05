using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Network
{
    public static class Helper
    {
        public static List<Address> ToNetworkAddresses(this List<string> addresses)
        {
            List<Address> addrs = new();
            foreach (var addr in addresses)
            {
                try
                {
                    addrs.Add(Network.Address.FromString(addr));
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(Helper), LogLevel.Warning, $"could not parse network address, address={addr}, error={e.Message}");
                    continue;
                }
            }
            if (addrs.Count == 0) throw new InvalidOperationException("no valid address");
            return addrs;
        }
    }
}
