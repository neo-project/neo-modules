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
            var from = tx.From.ToAddress();
            var to = tx.To.ToAddress();
            if (from == to) return;
            if (!txs.TryGetValue(from, out var m))
            {
                if (txs.TryGetValue(to, out m))
                {
                    to = from;
                    tx.Amount = BigInteger.Negate(tx.Amount);
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
            tgt.Amount += tx.Amount;
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
                var sign = tx.Amount.Sign;
                if (sign == 0) return;
                if (sign < 0)
                {
                    OwnerID temp = tx.From;
                    tx.From = tx.To;
                    tx.To = temp;
                    tx.Amount = BigInteger.Negate(tx.Amount);
                }
                e.Transfer(tx.From, tx.To, (long)tx.Amount, details);
            });
        }
    }
}
