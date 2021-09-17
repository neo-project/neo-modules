using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Neo.FileStorage.API.Audit;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class Calculator
    {
        private readonly SettlementDeps settlementDeps;

        public Calculator(SettlementDeps settlementDeps)
        {
            this.settlementDeps = settlementDeps;
        }

        public void Calculate(ulong epoch)
        {
            if (epoch == 0)
            {
                Utility.Log("Calculator", LogLevel.Info, "settlements are ignored for zero epoch");
                return;
            }
            Utility.Log("Calculator", LogLevel.Info, $"calculate audit settlements, epoch={epoch}");
            List<DataAuditResult> auditResults;
            try
            {
                auditResults = settlementDeps.AuditResultsForEpoch(epoch - 1);
            }
            catch (Exception e)
            {
                Utility.Log("Calculator", LogLevel.Debug, "could not collect audit results," + e);
                return;
            }
            if (!auditResults.Any())
            {
                Utility.Log("Calculator", LogLevel.Debug, "no audit results in previous epoch");
                return;
            }
            Utility.Log("Calculator", LogLevel.Debug, $"processing audit results, number={auditResults.Count}");
            var table = new TransferTable();
            foreach (var auditResult in auditResults)
            {
                ProcessResult(new SingleResultCtx()
                {
                    AuditResult = auditResult,
                    TxTable = table,
                    AuditFee = Settings.Default.AuditFee
                });
            }
            Utility.Log("Calculator", LogLevel.Debug, "processing transfers");
            TransferTable.TransferAssets(settlementDeps, table, Utility.StrictUTF8.GetBytes("settlement-audit"));
        }

        public void ProcessResult(SingleResultCtx ctx)
        {
            Utility.Log("Calculator", LogLevel.Debug, $"cid={ctx.ContainerId.String()}, audit_epoch={ctx.Epoch}");
            Utility.Log("Calculator", LogLevel.Debug, "reading information about the container");
            if (!ReadContainerInfo(ctx)) return;
            Utility.Log("Calculator", LogLevel.Debug, "building placement");
            if (!BuildPlacement(ctx)) return;
            Utility.Log("Calculator", LogLevel.Debug, "collecting passed nodes");
            if (!CollectPassNodes(ctx)) return;
            Utility.Log("Calculator", LogLevel.Debug, "calculating sum of the sizes of all storage groups");
            if (!SumSGSizes(ctx)) return;
            Utility.Log("Calculator", LogLevel.Debug, "filling transfer table");
            FillTransferTable(ctx);
        }

        public bool ReadContainerInfo(SingleResultCtx ctx)
        {
            try
            {
                ctx.Container = settlementDeps.ContainerInfo(ctx.ContainerId);
            }
            catch (Exception e)
            {
                Utility.Log("Calculator", LogLevel.Error, $"could not get container info, error={e}");
                return false;
            }
            return true;
        }

        public bool BuildPlacement(SingleResultCtx ctx)
        {
            try
            {
                ctx.ContainerNodes = settlementDeps.ContainerNodes(ctx.Epoch, ctx.ContainerId);
                var empty = ctx.ContainerNodes.Length == 0;
                Utility.Log("Calculator", LogLevel.Debug, "empty list of container nodes");
                return !empty;
            }
            catch (Exception e)
            {
                Utility.Log("Calculator", LogLevel.Error, $"could not get container nodes, error={e}");
                return false;
            }
        }

        public bool CollectPassNodes(SingleResultCtx ctx)
        {
            bool loopflag = false;
            foreach (var cnrNode in ctx.ContainerNodes)
            {
                foreach (var passNode in ctx.AuditResult.PassNodes)
                {
                    if (!cnrNode.PublicKey.SequenceEqual(passNode.ToByteArray())) continue;
                    foreach (var failNode in ctx.AuditResult.FailNodes)
                    {
                        if (cnrNode.PublicKey.SequenceEqual(failNode.ToByteArray()))
                        {
                            loopflag = true;
                            break;
                        }
                    }
                    if (loopflag) break;
                    ctx.PassedNodes[passNode.ToByteArray().ToHexString()] = cnrNode;
                }
            }
            if (ctx.PassedNodes.Count == 0)
            {
                Utility.Log("Calculator", LogLevel.Error, "none of the container nodes passed the audit");
                return false;
            }
            return true;
        }

        public bool SumSGSizes(SingleResultCtx ctx)
        {
            var passedSG = ctx.AuditResult.PassSg;
            if (passedSG.Count == 0)
            {
                Utility.Log("Calculator", LogLevel.Debug, "empty list of passed SG");
                return false;
            }
            ulong sumPassSGSize = 0;
            Address address = new();
            address.ContainerId = ctx.ContainerId;
            foreach (var sgID in ctx.AuditResult.PassSg)
            {
                try
                {
                    address.ObjectId = sgID;
                    var sgInfo = settlementDeps.SGInfo(address);
                    sumPassSGSize += sgInfo.ValidationDataSize;
                }
                catch
                {
                    Utility.Log("Calculator", LogLevel.Debug, $"could not get SG info, id={sgID}");
                    return false;
                }
            }
            if (sumPassSGSize == 0)
            {
                Utility.Log("Calculator", LogLevel.Debug, "zero sum SG size");
                return false;
            }
            ctx.SumSGSize = sumPassSGSize;
            return true;
        }

        public bool FillTransferTable(SingleResultCtx ctx)
        {
            var cnrOwner = ctx.Container.OwnerId;
            foreach (var item in ctx.PassedNodes)
            {
                OwnerID ownerID;
                try
                {
                    ownerID = OwnerID.FromScriptHash(item.Value.PublicKey.PublicKeyToScriptHash());
                }
                catch (Exception e)
                {
                    Utility.Log("Calculator", LogLevel.Error, $"could not resolve public key of the storage node, key={item.Key}, error={e}");
                    return false;
                }
                var price = item.Value.Price;
                Utility.Log("Calculator", LogLevel.Debug, $"calculating storage node salary for audit (GASe-12) sum SG, size={ctx.SumSGSize}, price={price}");
                var fee = BigInteger.Multiply(price, ctx.SumSGSize);
                fee = BigInteger.Divide(fee, BigInteger.One);
                if (fee.CompareTo(BigInteger.Zero) == 0) fee = BigInteger.Add(fee, BigInteger.One);
                ctx.TxTable.Transfer(new TransferTx() { From = cnrOwner, To = ownerID, Amount = fee });
            }
            var auditIR = OwnerID.FromScriptHash(ctx.AuditResult.PublicKey.ToByteArray().PublicKeyToScriptHash());
            ctx.TxTable.Transfer(new TransferTx() { From = cnrOwner, To = auditIR, Amount = ctx.AuditFee });
            return false;
        }
    }
}
