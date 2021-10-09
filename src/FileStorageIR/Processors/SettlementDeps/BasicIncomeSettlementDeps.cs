using System;
using System.Collections.Generic;
using System.Numerics;
using Google.Protobuf;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Invoker.Morph;
using Neo.IO;
using Neo.Wallets;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class BasicIncomeSettlementDeps : SettlementDeps
    {
        public static ulong BasicRate => Settings.Default.BasicIncomeRate;

        public override void Transfer(OwnerID sender, OwnerID recipient, long amount)
        {
            Transfer(sender, recipient, amount, Utility.StrictUTF8.GetBytes("settlement-basic-income"));
        }

        public BigInteger Balance(OwnerID id)
        {
            return Invoker.BalanceOf(id.ToScriptHash().ToArray());
        }

        public Estimations[] Estimations(ulong epoch)
        {
            List<byte[]> estimationIDs = Invoker.ListSizes(epoch);
            List<Estimations> result = new();
            foreach (var estimationID in estimationIDs)
            {
                try
                {
                    ContainerID id = new();
                    id.Value = ByteString.CopyFrom(estimationID);
                    Estimations estimation = Invoker.GetContainerSize(id);
                    result.Add(estimation);
                }
                catch (Exception e)
                {
                    Utility.Log("BasicIncomeSettlementDeps", LogLevel.Warning, string.Format("can't get used space estimation,estimation_id:{0},error:{1}", estimationID.ToHexString(), e.Message));
                }
            }
            return result.ToArray();
        }
    }
}
