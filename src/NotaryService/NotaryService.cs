using Akka.Actor;
using Neo;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins
{
    public class NotaryService : UntypedActor
    {
        public class Start { }
        private readonly NeoSystem neoSystem;
        private readonly Wallet wallet;
        private bool started;
        private readonly NotaryRequestPool pool;
        private readonly ConcurrentDictionary<UInt256, NotaryTask> pendingQueue = new();
        private readonly Queue<NotaryRequest> payloadCache = new();

        public NotaryService(NeoSystem neoSystem, Wallet wallet)
        {
            this.wallet = wallet;
            this.neoSystem = neoSystem;
            pool = new(neoSystem, Settings.Default.Capacity);
        }

        protected override void OnReceive(object message)
        {
            if (message is Start)
            {
                if (started) return;
                OnStart();
            }
            else
            {
                if (!started) return;
                switch (message)
                {
                    case Blockchain.PersistCompleted completed:
                        OnPersistCompleted(completed.Block);
                        break;
                    case Blockchain.RelayResult rr:
                        if (rr.Result == VerifyResult.Succeed && rr.Inventory is NotaryRequest payload)
                            OnNotaryPayload(payload);
                        break;
                }
            }
        }

        private void OnStart()
        {
            Log("OnStart");
            started = true;
        }

        private void OnNotaryPayload(NotaryRequest payload)
        {
            if (pool.TryAdd(payload, out var removed))
            {
                OnNewRequest(payload);
                if (removed is not null) OnRequestRemoval(removed);
            }
        }

        private void OnPersistCompleted(Block block)
        {
            Log($"Persisted {nameof(Block)}: height={block.Index} hash={block.Hash} tx={block.Transactions.Length}");
            var currHeight = block.Index;
            foreach (var (_, r) in pendingQueue)
            {
                if (!r.IsSent && r.IsMainCompleted() && r.MinNotValidBefore > currHeight)
                {
                    Finalize(r.MainTx);
                    r.IsSent = true;
                    continue;
                }
                if (r.MinNotValidBefore <= currHeight)
                {
                    foreach (var fb in r.FallbackTxs)
                    {
                        var nvb = fb.GetAttributes<NotValidBefore>().ToArray()[0].Height;
                        if (nvb <= currHeight) Finalize(fb);
                    }
                }
            }
            foreach (var n in pool.ReVerify(block.Transactions))
                OnRequestRemoval(n);
        }

        private void OnNewRequest(NotaryRequest payload)
        {
            var snapshot = neoSystem.GetSnapshot();
            var nvbFallback = payload.FallbackTransaction.GetAttributes<NotValidBefore>().ToArray()[0].Height;
            byte nKeys = payload.MainTransaction.GetAttributes<NotaryAssisted>().ToArray()[0].NKeys;
            VerifyIncompleteWitnesses(payload.MainTransaction, nKeys, out WitnessInfo[] newInfo, out string validationErr);
            if (validationErr is not null)
                Log($"verification of main notary transaction failed; fallback transaction will be completed,main hash:{payload.MainTransaction.Hash},fallback hash:{payload.FallbackTransaction.Hash},verification error:{validationErr}");
            var exists = pendingQueue.TryGetValue(payload.MainTransaction.Hash, out NotaryTask r);
            if (exists)
            {
                if (r.FallbackTxs.Any(fb => fb.Hash.Equals(payload.FallbackTransaction.Hash))) return;
                if (nvbFallback < r.MinNotValidBefore)
                    r.MinNotValidBefore = nvbFallback;
            }
            else
            {
                r = new NotaryTask()
                {
                    MainTx = payload.MainTransaction,
                    MinNotValidBefore = nvbFallback,
                    FallbackTxs = Array.Empty<Transaction>(),
                };
                pendingQueue[payload.MainTransaction.Hash] = r;
            }
            if (r.WitnessInfo is null && validationErr is null)
                r.WitnessInfo = newInfo;
            r.FallbackTxs = r.FallbackTxs.Append(payload.FallbackTransaction).ToArray();
            if (exists && r.IsMainCompleted() || validationErr is not null)
                return;
            var mainHash = r.MainTx.GetSignData(neoSystem.Settings.Network);
            for (int i = 0; i < payload.MainTransaction.Witnesses.Length; i++)
            {
                if (payload.MainTransaction.Witnesses[i].InvocationScript.Length == 0 || r.WitnessInfo[i].NSigsLeft == 0 && r.WitnessInfo[i].Typ != RequestType.Contract)
                    continue;
                switch (r.WitnessInfo[i].Typ)
                {
                    case RequestType.Contract:
                        if (!r.MainTx.VerifyWitness(neoSystem.Settings, snapshot, r.MainTx.Signers[i].Account, payload.MainTransaction.Witnesses[i], r.MainTx.NetworkFee, out _))
                            continue;
                        r.MainTx.Witnesses[i].InvocationScript = payload.MainTransaction.Witnesses[i].InvocationScript;
                        break;
                    case RequestType.Signature:
                        if (Crypto.VerifySignature(mainHash, payload.MainTransaction.Witnesses[i].InvocationScript.Skip(2).ToArray(), r.WitnessInfo[i].Pubs[0]))
                        {
                            r.MainTx.Witnesses[i] = payload.MainTransaction.Witnesses[i];
                            r.WitnessInfo[i].NSigsLeft--;
                        }
                        break;
                    case RequestType.MultiSignature:
                        r.WitnessInfo[i].Sigs ??= new Dictionary<ECPoint, byte[]>();
                        foreach (var pub in r.WitnessInfo[i].Pubs)
                        {
                            if (r.WitnessInfo[i].Sigs.ContainsKey(pub))
                                continue;
                            if (Crypto.VerifySignature(mainHash, payload.MainTransaction.Witnesses[i].InvocationScript.Skip(2).ToArray(), pub))
                            {
                                r.WitnessInfo[i].Sigs[pub] = payload.MainTransaction.Witnesses[i].InvocationScript;
                                r.WitnessInfo[i].NSigsLeft--;
                                if (r.WitnessInfo[i].NSigsLeft == 0)
                                {
                                    byte[] invScript = Array.Empty<byte>();
                                    foreach (var t in r.WitnessInfo[i].Pubs)
                                    {
                                        if (r.WitnessInfo[i].Sigs.TryGetValue(t, out var sig))
                                            invScript = invScript.Concat(sig).ToArray();
                                    }
                                    r.MainTx.Witnesses[i].InvocationScript = invScript;
                                }
                                break;
                            }
                        }
                        break;
                }
                var currentHeight = NativeContract.Ledger.CurrentIndex(snapshot);
                if (r.IsMainCompleted() && r.MinNotValidBefore > currentHeight)
                {
                    Finalize(r.MainTx);
                    r.IsSent = true;
                }
            }
        }

        private void OnRequestRemoval(NotaryRequest payload)
        {
            var r = pendingQueue[payload.MainTransaction.Hash];
            if (r is null) return;
            for (int i = 0; i < r.FallbackTxs.Length; i++)
            {
                var fb = r.FallbackTxs[i];
                if (fb.Hash.Equals(payload.FallbackTransaction.Hash))
                {
                    var tempList = r.FallbackTxs.ToList();
                    tempList.RemoveAt(i);
                    r.FallbackTxs = tempList.ToArray();
                    break;
                }
            }
            if (r.FallbackTxs.Length == 0) pendingQueue.Remove(r.MainTx.Hash, out _);
        }

        private void VerifyIncompleteWitnesses(Transaction tx, byte nKeysExpected, out WitnessInfo[] witnessInfos, out string validationErr)
        {
            int nKeysActual = 0;
            witnessInfos = null;
            validationErr = null;
            if (tx.Signers.Length < 2)
            {
                validationErr = "transaction should have at least 2 signers";
                return;
            }
            if (tx.Signers.Any(p => p.Equals(NativeContract.Notary.Hash)))
            {
                validationErr = "P2PNotary contract should be a signer of the transaction";
                return;
            }
            witnessInfos = new WitnessInfo[tx.Signers.Length];
            for (int i = 0; i < tx.Witnesses.Length; i++)
            {
                if (tx.Witnesses[i].VerificationScript.Length == 0)
                {
                    witnessInfos[i] = new WitnessInfo()
                    {
                        Typ = RequestType.Contract,
                        NSigsLeft = 0
                    };
                    continue;
                }
                if (!tx.Signers[i].Account.Equals(tx.Witnesses[i].VerificationScript.ToScriptHash()))
                {
                    validationErr = $"transaction should have valid verification script for signer {i}";
                    witnessInfos = null;
                    return;
                }
                if (tx.Witnesses[i].InvocationScript.Length != 0)
                {
                    if (tx.Witnesses[i].InvocationScript.Length != 66 || (tx.Witnesses[i].InvocationScript[0] != (byte)OpCode.PUSHDATA1 && tx.Witnesses[i].InvocationScript[1] != 64))
                    {
                        validationErr = "multisignature invocation script should have length = 66 and be of the form[PUSHDATA1, 64, signatureBytes...]";
                        witnessInfos = null;
                        return;
                    }
                }
                if (tx.Witnesses[i].VerificationScript.IsMultiSigContract(out int nSigs, out ECPoint[] pubs))
                {
                    witnessInfos[i] = new WitnessInfo()
                    {
                        Typ = RequestType.MultiSignature,
                        NSigsLeft = (byte)nSigs,
                        Pubs = pubs,
                    };
                    nKeysActual += pubs.Length;
                    continue;
                }
                if (tx.Witnesses[i].VerificationScript.IsSignatureContract())
                {
                    witnessInfos[i] = new WitnessInfo()
                    {
                        Typ = RequestType.Signature,
                        NSigsLeft = 1,
                        Pubs = new ECPoint[pubs.Length],
                    };
                    nKeysActual++;
                    continue;
                }
                validationErr = $"witness {i}: unable to detect witness type, only sig/multisig/contract are supported";
                witnessInfos = null;
                return;
            }
            if (nKeysActual != nKeysExpected)
            {
                validationErr = $"expected and actual NKeys mismatch: {nKeysExpected} vs {nKeysActual}";
                witnessInfos = null;
                return;
            }
            return;
        }

        private void Finalize(Transaction tx)
        {
            var prefix = new byte[] { (byte)OpCode.PUSHDATA1, 64 };
            var notaryWitness = new Witness()
            {
                InvocationScript = prefix.Concat(tx.Sign(wallet.GetAccounts().ToArray()[0].GetKey(), neoSystem.Settings.Network)).ToArray(),
                VerificationScript = new byte[0]
            };
            for (int i = 0; i < tx.Signers.Length; i++)
                if (tx.Signers[i].Account.Equals(NativeContract.Notary.Hash))
                {
                    tx.Witnesses[i] = notaryWitness;
                    break;
                }
            neoSystem.Blockchain.Tell(tx);
        }

        private static void Log(string message, LogLevel level = LogLevel.Info)
        {
            Utility.Log(nameof(NotaryService), level, message);
        }

        public static Props Props(NeoSystem neoSystem, Wallet wallet)
        {
            return Akka.Actor.Props.Create(() => new NotaryService(neoSystem, wallet));
        }

        public class NotaryTask
        {
            public bool IsSent;
            public Transaction MainTx;
            public Transaction[] FallbackTxs;
            public uint MinNotValidBefore;
            public WitnessInfo[] WitnessInfo;

            public bool IsMainCompleted()
            {
                return WitnessInfo is not null && WitnessInfo.All(wi => wi.NSigsLeft == 0);
            }
        }

        public class WitnessInfo
        {
            public RequestType Typ;
            public byte NSigsLeft;
            public ECPoint[] Pubs;
            public Dictionary<ECPoint, byte[]> Sigs;
        }

        public enum RequestType : byte
        {
            Signature = 0x01,
            MultiSignature = 0x02,
            Contract = 0x03
        }
    }
}
