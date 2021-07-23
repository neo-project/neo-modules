using System;
using System.Collections.Generic;
using System.Numerics;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class TransferTable
    {
        private readonly Dictionary<string, Dictionary<string, TransferTx>> txs = new();

        public void Transfer(TransferTx tx)
        {
            var from = tx.from.ToBase58String();
            var to = tx.to.ToBase58String();
            if (from == to) return;
            if (!txs.TryGetValue(from, out var m))
            {
                if (txs.TryGetValue(to, out m))
                {
                    to = from;
                    tx.amount = BigInteger.Negate(tx.amount);
                }
                else
                {
                    m = new Dictionary<string, TransferTx>();
                    txs[from] = m;
                }
            }
            if (!m.TryGetValue(to, out var tgt))
            {
                m[to] = tx;
                return;
            }
            tgt.amount += tx.amount;
        }

        public void Iterate(Action<TransferTx> f)
        {
            foreach (var m in txs)
                foreach (var tx in m.Value)
                    f(tx.Value);
        }

        public static void TransferAssets(SettlementDeps e, TransferTable t, byte[] details)
        {
            t.Iterate((TransferTx tx) =>
            {
                var sign = tx.amount.Sign;
                if (sign == 0) return;
                if (sign < 0)
                {
                    OwnerID temp = tx.from;
                    tx.from = tx.to;
                    tx.to = temp;
                    tx.amount = BigInteger.Negate(tx.amount);
                }
                e.Transfer(tx.from, tx.to, (long)tx.amount, details);
            });
        }
    }
}
