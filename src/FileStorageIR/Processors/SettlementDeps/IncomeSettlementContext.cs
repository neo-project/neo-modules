using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Invoker.Morph;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class IncomeSettlementContext
    {
        private static readonly UInt160 BankAccount = new(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
        private readonly OwnerID bankOwner;
        private readonly object lockObject = new();
        private readonly NodeSizeTable distributeTable = new();
        public BasicIncomeSettlementDeps SettlementDeps { get; init; }
        public ulong Epoch { get; init; }

        public IncomeSettlementContext()
        {
            bankOwner = OwnerID.FromScriptHash(BankAccount);
        }

        public void Collect()
        {
            lock (lockObject)
            {
                var cachedRate = BasicIncomeSettlementDeps.BasicRate;
                var cnrEstimations = SettlementDeps.Estimations(Epoch);
                var txTable = new TransferTable();
                foreach (var item in cnrEstimations)
                {
                    OwnerID owner = null;
                    try
                    {
                        owner = SettlementDeps.ContainerInfo(item.ContainerID).OwnerId;
                    }
                    catch
                    {
                        Utility.Log("IncomeSettlementContext", LogLevel.Info, $"can't fetch container info, Epoch={Epoch}, container_id={item.ContainerID.String()}");
                        continue;
                    }
                    Node[] cnrNodes = null;
                    try
                    {
                        cnrNodes = SettlementDeps.ContainerNodes(Epoch, item.ContainerID);
                    }
                    catch
                    {
                        Utility.Log("IncomeSettlementContext", LogLevel.Info, $"can't fetch container info, Epoch={Epoch}, container_id={item.ContainerID.String()}");
                        continue;
                    }
                    ulong avg = AvgEstimation(item);
                    BigInteger total = CalculateBasicSum(avg, cachedRate, cnrNodes.Length);
                    foreach (var node in cnrNodes)
                        distributeTable.Put(node.PublicKey, avg);
                    txTable.Transfer(new TransferTx() { From = owner, To = bankOwner, Amount = total });
                }
                TransferTable.TransferAssets(SettlementDeps, txTable, Utility.StrictUTF8.GetBytes("settlement-basincome"));
            }
        }

        public static ulong AvgEstimation(Estimations e)
        {
            ulong avg = 0;
            if (!e.AllEstimation.Any()) return avg;
            foreach (var estimation in e.AllEstimation)
                avg += estimation.Size;
            return avg / (ulong)e.AllEstimation.Count;
        }

        public static BigInteger CalculateBasicSum(ulong size, ulong rate, int ln)
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
                BigInteger bankBalance = SettlementDeps.Balance(bankOwner);
                BigInteger total = distributeTable.Total();
                distributeTable.Iterate((byte[] key, BigInteger n) =>
                {
                    var nodeOwner = OwnerID.FromScriptHash(key.PublicKeyToScriptHash());
                    txTable.Transfer(new TransferTx() { From = bankOwner, To = nodeOwner, Amount = NormalizedValue(n, total, bankBalance) });
                });
                TransferTable.TransferAssets(SettlementDeps, txTable, Utility.StrictUTF8.GetBytes("settlement-basincome"));
            }
        }

        public static BigInteger NormalizedValue(BigInteger n, BigInteger total, BigInteger limit)
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
