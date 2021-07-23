using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Invoker.Morph;
using Neo.IO;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class IncomeSettlementContext
    {
        private readonly object lockObject = new();
        public BasicIncomeSettlementDeps settlementDeps;
        public ulong epoch;
        public OwnerID bankOwner;
        private readonly NodeSizeTable distributeTable = new();

        public OwnerID BankOwnerID()
        {
            OwnerID ownerID = new();
            UInt160 account = new(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
            ownerID.Value = ByteString.CopyFrom(Cryptography.Base58.Decode(Cryptography.Base58.Base58CheckEncode(account.ToArray())));
            return ownerID;
        }

        public void Collect()
        {
            lock (lockObject)
            {
                var cachedRate = settlementDeps.BasicRate;
                var cnrEstimations = settlementDeps.Estimations(epoch);
                var txTable = new TransferTable();
                foreach (var item in cnrEstimations)
                {
                    OwnerID owner = null;
                    try
                    {
                        owner = settlementDeps.ContainerInfo(item.ContainerID).OwnerId;
                    }
                    catch
                    {
                        Utility.Log("IncomeSettlementContext", LogLevel.Info, $"can't fetch container info, epoch={epoch}, container_id={item.ContainerID.ToBase58String()}");
                        continue;
                    }
                    Node[] cnrNodes = null;
                    try
                    {
                        cnrNodes = settlementDeps.ContainerNodes(epoch, item.ContainerID);
                    }
                    catch
                    {
                        Utility.Log("IncomeSettlementContext", LogLevel.Info, $"can't fetch container info, epoch={epoch}, container_id={item.ContainerID.ToBase58String()}");
                        continue;
                    }
                    ulong avg = AvgEstimation(item);
                    BigInteger total = CalculateBasicSum(avg, cachedRate, cnrNodes.Length);
                    foreach (var node in cnrNodes)
                        distributeTable.Put(node.PublicKey, avg);
                    txTable.Transfer(new TransferTx() { from = owner, to = BankOwnerID(), amount = total });
                }
                TransferTable.TransferAssets(settlementDeps, txTable, Utility.StrictUTF8.GetBytes("settlement-basincome"));
            }
        }

        public ulong AvgEstimation(Estimations e)
        {
            ulong avg = 0;
            if (!e.AllEstimation.Any()) return avg;
            foreach (var estimation in e.AllEstimation)
                avg += estimation.Size;
            return avg / (ulong)e.AllEstimation.Count;
        }

        public BigInteger CalculateBasicSum(ulong size, ulong rate, int ln)
        {
            BigInteger bigRate = rate;
            ulong total = size * (ulong)ln;
            BigInteger price = new(total);
            price = BigInteger.Multiply(price, bigRate);
            price = BigInteger.Divide(price, BaseProcessor.BigGB);
            if (price.CompareTo(BaseProcessor.BigZero) == 0)
                price = BigInteger.Add(price, BaseProcessor.BigOne);
            return price;
        }

        public void Distribute()
        {
            lock (lockObject)
            {
                var txTable = new TransferTable();
                BigInteger bankBalance = settlementDeps.Balance(bankOwner);
                BigInteger total = distributeTable.Total();
                distributeTable.Iterate((byte[] key, BigInteger n) =>
                {
                    var nodeOwner = key.PublicKeyToOwnerID();
                    txTable.Transfer(new TransferTx() { from = bankOwner, to = nodeOwner, amount = NormalizedValue(n, total, bankBalance) });
                });
                TransferTable.TransferAssets(settlementDeps, txTable, Utility.StrictUTF8.GetBytes("settlement-basincome"));
            }
        }

        public BigInteger NormalizedValue(BigInteger n, BigInteger total, BigInteger limit)
        {
            if (limit.CompareTo(BaseProcessor.BigZero) == 0) return 0;
            n = BigInteger.Multiply(n, limit);
            return BigInteger.Divide(n, total);
        }

        private class NodeSizeTable
        {
            private readonly Dictionary<string, ulong> price = new();
            private ulong total;

            public void Put(byte[] id, ulong avg)
            {
                price[Utility.StrictUTF8.GetString(id)] += avg;
                total += avg;
            }

            public BigInteger Total() => total;

            public void Iterate(Action<byte[], BigInteger> f)
            {
                foreach (var item in price)
                    f(Utility.StrictUTF8.GetBytes(item.Key), item.Value);
            }
        }
    }
}
