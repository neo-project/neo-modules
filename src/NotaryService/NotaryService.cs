using Akka.Actor;
using Neo;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
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
        private NeoSystem neoSystem;
        private Wallet wallet;
        private bool started;

        private readonly ConcurrentDictionary<UInt256, NotaryTask> pendingQueue = new ConcurrentDictionary<UInt256, NotaryTask>();
        private readonly List<NotaryRequest> payloadCache = new List<NotaryRequest>();

        public NotaryService(NeoSystem neoSystem, Wallet wallet)
        {
            this.wallet = wallet;
            this.neoSystem = neoSystem;
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
            if (payloadCache.Count < 100)
            {
                payloadCache.Add(payload);
                OnNewRequest(payload);
            }
            else
            {
                var oldPayload = payloadCache[0];
                payloadCache.RemoveAt(0);
                OnRequestRemoval(oldPayload);
                OnNewRequest(payload);
            }
        }

        private void OnPersistCompleted(Block block)
        {
            Log($"Persisted {nameof(Block)}: height={block.Index} hash={block.Hash} tx={block.Transactions.Length}");
            var currHeight = block.Index;
            foreach (var item in pendingQueue)
            {
                var h = item.Key;
                var r = item.Value;
                if (!r.isSent && r.IsMainCompleted() && r.minNotValidBefore > currHeight)
                {
                    Finalize(r.mainTx);
                    r.isSent = true;
                    continue;
                }
                if (r.minNotValidBefore <= currHeight)
                {
                    var newFallbacks = new List<Transaction>();
                    foreach (var fb in r.fallbackTxs)
                    {
                        var nvb = fb.GetAttributes<NotValidBefore>().ToArray()[0].Height;
                        if (nvb <= currHeight) Finalize(fb);
                    }
                }
            }
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
                foreach (var fb in r.fallbackTxs)
                    if (fb.Hash.Equals(payload.FallbackTransaction.Hash))
                        return;
                if (nvbFallback < r.minNotValidBefore)
                    r.minNotValidBefore = nvbFallback;
            }
            else
            {
                r = new NotaryTask()
                {
                    mainTx = payload.MainTransaction,
                    minNotValidBefore = nvbFallback,
                    fallbackTxs = new Transaction[0],
                };
                pendingQueue[payload.MainTransaction.Hash] = r;
            }
            if (r.witnessInfo is null && validationErr is null)
                r.witnessInfo = newInfo;
            r.fallbackTxs = r.fallbackTxs.Append(payload.FallbackTransaction).ToArray();
            if (exists && r.IsMainCompleted() || validationErr is not null)
                return;
            var mainHash = r.mainTx.GetSignData(neoSystem.Settings.Network);
            for (int i = 0; i < payload.MainTransaction.Witnesses.Length; i++)
            {
                if (payload.MainTransaction.Witnesses[i].InvocationScript.Length == 0 || r.witnessInfo[i].nSigsLeft == 0 && r.witnessInfo[i].typ != RequestType.Contract)
                    continue;
                switch (r.witnessInfo[i].typ)
                {
                    case RequestType.Contract:
                        if (!VerifyWitness(r.mainTx, neoSystem.Settings, snapshot, r.mainTx.Signers[i].Account, payload.MainTransaction.Witnesses[i], r.mainTx.NetworkFee, out _))
                            continue;
                        r.mainTx.Witnesses[i].InvocationScript = payload.MainTransaction.Witnesses[i].InvocationScript;
                        break;
                    case RequestType.Signature:
                        if (Crypto.VerifySignature(mainHash, payload.MainTransaction.Witnesses[i].InvocationScript.Skip(2).ToArray(), r.witnessInfo[i].pubs[0]))
                        {
                            r.mainTx.Witnesses[i] = payload.MainTransaction.Witnesses[i];
                            r.witnessInfo[i].nSigsLeft--;
                        }
                        break;
                    case RequestType.MultiSignature:
                        if (r.witnessInfo[i].sigs is null)
                            r.witnessInfo[i].sigs = new Dictionary<ECPoint, byte[]>();
                        foreach (var pub in r.witnessInfo[i].pubs)
                        {
                            if (r.witnessInfo[i].sigs.TryGetValue(pub, out _))
                                continue;
                            if (Crypto.VerifySignature(mainHash, payload.MainTransaction.Witnesses[i].InvocationScript.Skip(2).ToArray(), pub))
                            {
                                r.witnessInfo[i].sigs[pub] = payload.MainTransaction.Witnesses[i].InvocationScript;
                                r.witnessInfo[i].nSigsLeft--;
                                if (r.witnessInfo[i].nSigsLeft == 0)
                                {
                                    byte[] invScript = new byte[0];
                                    for (int j = 0; j < r.witnessInfo[i].pubs.Length; j++)
                                    {
                                        if (r.witnessInfo[i].sigs.TryGetValue(r.witnessInfo[i].pubs[j], out var sig))
                                            invScript = invScript.Concat(sig).ToArray();
                                    }
                                    r.mainTx.Witnesses[i].InvocationScript = invScript;
                                }
                                break;
                            }
                        }
                        break;
                }
                var currentHeight = NativeContract.Ledger.CurrentIndex(snapshot);
                if (r.IsMainCompleted() && r.minNotValidBefore > currentHeight)
                {
                    Finalize(r.mainTx);
                    r.isSent = true;
                }
            }
        }

        private void OnRequestRemoval(NotaryRequest payload)
        {
            var r = pendingQueue[payload.MainTransaction.Hash];
            if (r is null) return;
            for (int i = 0; i < r.fallbackTxs.Length; i++)
            {
                var fb = r.fallbackTxs[i];
                if (fb.Hash.Equals(payload.FallbackTransaction.Hash))
                {
                    var tempList = r.fallbackTxs.ToList();
                    tempList.RemoveAt(i);
                    r.fallbackTxs = tempList.ToArray();
                    break;
                }
            }
            if (r.fallbackTxs.Length == 0) pendingQueue.Remove(r.mainTx.Hash, out _);
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
                        typ = RequestType.Contract,
                        nSigsLeft = 0
                    };
                    continue;
                }
                if (!tx.Signers[i].Account.Equals(tx.Witnesses[i].VerificationScript.ToScriptHash()))
                {
                    validationErr = string.Format("transaction should have valid verification script for signer {0}", i);
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
                        typ = RequestType.MultiSignature,
                        nSigsLeft = (byte)nSigs,
                        pubs = new ECPoint[pubs.Length],
                    };
                    for (int j = 0; j < pubs.Length; j++)
                    {
                        witnessInfos[i].pubs[j] = pubs[j];
                    }
                    nKeysActual += pubs.Length;
                    continue;
                }
                if (tx.Witnesses[i].VerificationScript.IsSignatureContract())
                {
                    witnessInfos[i] = new WitnessInfo()
                    {
                        typ = RequestType.Signature,
                        nSigsLeft = 1,
                        pubs = new ECPoint[pubs.Length],
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

        private bool VerifyWitness(IVerifiable verifiable, ProtocolSettings settings, DataCache snapshot, UInt160 hash, Witness witness, long gas, out long fee)
        {
            fee = 0;
            Script invocationScript;
            try
            {
                invocationScript = new Script(witness.InvocationScript, true);
            }
            catch (BadScriptException)
            {
                return false;
            }
            using (ApplicationEngine engine = ApplicationEngine.Create(TriggerType.Verification, verifiable, snapshot?.CreateSnapshot(), null, settings, gas))
            {
                if (witness.VerificationScript.Length == 0)
                {
                    ContractState cs = NativeContract.ContractManagement.GetContract(snapshot, hash);
                    if (cs is null) return false;
                    ContractMethodDescriptor md = cs.Manifest.Abi.GetMethod("verify", -1);
                    if (md?.ReturnType != ContractParameterType.Boolean) return false;
                    engine.LoadContract(cs, md, CallFlags.ReadOnly);
                }
                else
                {
                    if (NativeContract.IsNative(hash)) return false;
                    if (hash != witness.ScriptHash) return false;
                    Script verificationScript;
                    try
                    {
                        verificationScript = new Script(witness.VerificationScript, true);
                    }
                    catch (BadScriptException)
                    {
                        return false;
                    }
                    engine.LoadScript(verificationScript, initialPosition: 0, configureState: p =>
                    {
                        p.CallFlags = CallFlags.ReadOnly;
                        p.ScriptHash = hash;
                    });
                }

                engine.LoadScript(invocationScript, configureState: p => p.CallFlags = CallFlags.None);

                if (engine.Execute() == VMState.FAULT) return false;
                if (!engine.ResultStack.Peek().GetBoolean()) return false;
                fee = engine.GasConsumed;
            }
            return true;
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
            public bool isSent;
            public Transaction mainTx;
            public Transaction[] fallbackTxs;
            public uint minNotValidBefore;
            public WitnessInfo[] witnessInfo;

            public bool IsMainCompleted()
            {
                if (witnessInfo is null) return false;
                foreach (var wi in witnessInfo) if (wi.nSigsLeft != 0) return false;
                return true;
            }
        }

        public class WitnessInfo
        {
            public RequestType typ;
            public byte nSigsLeft;
            public ECPoint[] pubs;
            public Dictionary<ECPoint, byte[]> sigs;
        }

        public enum RequestType : byte
        {
            Unknown = 0x00,
            Signature = 0x01,
            MultiSignature = 0x02,
            Contract = 0x03
        }
    }
}
