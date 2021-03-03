using Akka.Actor;
using Neo.IO;
using Neo.IO.Data.LevelDB;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;
using System.Linq;

namespace Neo.Plugins.MultiSigInbox
{
    class MultiSigInboxService : UntypedActor
    {
        private readonly NeoSystem _system;
        private readonly DB _db;
        private readonly Wallet _wallet;
        private bool started = false;
        public class Start { }

        public MultiSigInboxService(NeoSystem system, DB db, Wallet wallet)
           : this(system)
        {
            this._db = db;
            this._wallet = wallet;
        }

        internal MultiSigInboxService(NeoSystem system)
        {
            _system = system;
            Context.System.EventStream.Subscribe(Self, typeof(Blockchain.PersistCompleted));
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
                    case Blockchain.RelayResult rr:
                        if (rr.Result == VerifyResult.Succeed)
                        {
                            if (rr.Inventory is ExtensiblePayload payload && payload.Category == "MultiSigInbox")
                                OnExtensiblePayload(payload);
                            else if (rr.Inventory is Transaction tx)
                                OnVerifiedTransaction(tx);
                        }
                        break;
                }
            }
        }

        private void OnVerifiedTransaction(Transaction tx)
        {
            // Remove tx from inbox
            _db.Delete(WriteOptions.Default, MultiSigInboxPlugin.TxPrefix.Concat(tx.Hash.ToArray()).ToArray());
        }

        private void OnExtensiblePayload(ExtensiblePayload payload)
        {
            // Add to inbox

            using var snapshot = _system.GetSnapshot();
            var newContext = ContractParametersContext.FromJson(JObject.Parse(payload.Data), snapshot);
            if (newContext.Completed) return;

            var tx = _db.Get(ReadOptions.Default, MultiSigInboxPlugin.TxPrefix.Concat(newContext.Verifiable.Hash.ToArray()).ToArray());
            if (tx != null)
            {
                // merge context
                var somethingAdded = false;
                var oldContext = ContractParametersContext.FromJson(JObject.Parse(tx), snapshot);

                foreach (var entry in oldContext.ScriptHashes)
                {
                    var contract = _wallet.GetAccount(entry)?.Contract;
                    if (contract == null) continue;

                    var newSignatures = newContext.GetSignatures(entry);
                    if (newSignatures == null) continue;

                    var oldSignatures = oldContext.GetSignatures(entry)?.ToDictionary(u => (u.pubKey, u.signature));
                    foreach (var sig in newSignatures)
                    {
                        if (oldSignatures?.ContainsKey(sig.pubKey) == true) continue;

                        if (oldContext.AddSignature(contract, sig.pubKey, sig.signature))
                        {
                            somethingAdded = true;
                        }
                    }
                }

                if (somethingAdded)
                {
                    _db.Put(WriteOptions.Default, MultiSigInboxPlugin.TxPrefix.Concat(oldContext.Verifiable.Hash.ToArray()).ToArray(), oldContext.ToJson().ToByteArray(false));
                }
            }
            else
            {
                // new context
                _db.Put(WriteOptions.Default, MultiSigInboxPlugin.TxPrefix.Concat(newContext.Verifiable.Hash.ToArray()).ToArray(), newContext.ToJson().ToByteArray(false));
            }
        }

        private static void Log(string message, LogLevel level = LogLevel.Info)
        {
            Utility.Log(nameof(MultiSigInboxService), level, message);
        }

        private void OnStart()
        {
            Log("OnStart");
            started = true;
        }

        protected override void PostStop()
        {
            Log("OnStop");
            started = false;
            Context.System.EventStream.Unsubscribe(Self);
            base.PostStop();
        }

        public static Props Props(NeoSystem system, DB db, Wallet wallet)
        {
            return Akka.Actor.Props.Create(() => new MultiSigInboxService(system, db, wallet));
        }
    }
}
