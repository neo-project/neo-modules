using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Neo.FileStorage.Services.Reputaion.Local.Storage;
using static Neo.Utility;

namespace Neo.FileStorage.Services.Reputaion.Local.Control
{
    public class ReportContext
    {
        public CancellationToken Cancel;
        public ulong Epoch;
        public Controller Controller;

        public void Report()
        {
            TrustStorage ts = null;
            try
            {
                ts = Controller.ReputationStorage.DataForEpoch(Epoch);
            }
            catch (Exception e)
            {
                if (e is not KeyNotFoundException)
                    throw;
            }
            if (ts is not null)
            {
                ts.Iterate(t =>
                {
                    Log("Reputation", LogLevel.Debug, $"local trust, epoch={Epoch}, peer={t.PeerId}, value={t.Value}");
                });
            }
            else
            {
                var nm = Controller.NetmapCache.GetNetMapByEpoch(Epoch);
                int localIndex = -1;
                for (int i = 0; i < nm.Nodes.Count; i++)
                {
                    if (Controller.LocalKey.SequenceEqual(nm.Nodes[i].PublicKey))
                    {
                        localIndex = i;
                        break;
                    }
                }
                var ln = nm.Nodes.Count;
                if (0 <= localIndex && 0 < ln) ln--;
                double p = 1.0 / ln;
                for (int i = 0; i < nm.Nodes.Count; i++)
                {
                    if (i == localIndex) continue;
                    Trust t = new()
                    {
                        PeerId = new(nm.Nodes[i].PublicKey),
                        Value = p,
                    };
                    Log("Reputation", LogLevel.Debug, $"local trust, epoch={Epoch}, peer={t.PeerId}, value={t.Value}");
                }
            }
        }
    }
}
