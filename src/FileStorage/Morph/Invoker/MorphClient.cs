using Akka.Actor;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.FileStorage.Morph.Invoker
{
    /// <summary>
    /// MorphClient is an implementation of the IClient interface.
    /// It is used to pre-execute invoking script and send script to the morph chain.
    /// </summary>
    public class MorphClient : IClient
    {
        public Wallet wallet;
        public NeoSystem system;
        public IActorRef actor;

        public Wallet GetWallet() => wallet;
        public class FakeSigners : IVerifiable
        {
            private readonly UInt160[] _hashForVerify;
            Witness[] IVerifiable.Witnesses { get; set; }

            int ISerializable.Size => throw new NotImplementedException();

            void ISerializable.Deserialize(BinaryReader reader)
            {
                throw new NotImplementedException();
            }

            void IVerifiable.DeserializeUnsigned(BinaryReader reader)
            {
                throw new NotImplementedException();
            }

            public FakeSigners(params UInt160[] hashForVerify)
            {
                _hashForVerify = hashForVerify ?? new UInt160[0];
            }

            UInt160[] IVerifiable.GetScriptHashesForVerifying(DataCache snapshot)
            {
                return _hashForVerify;
            }

            void ISerializable.Serialize(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }

            void IVerifiable.SerializeUnsigned(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }
        }

        public bool Invoke(out UInt256 txId, UInt160 contractHash, string method, long fee, params object[] args)
        {
            txId = null;
            InvokeResult result = TestInvoke(contractHash, method, args);
            if (result.State != VMState.HALT) return false;
            SnapshotCache snapshot = system.GetSnapshot();
            uint height = NativeContract.Ledger.CurrentIndex(snapshot);
            Random rand = new Random();
            Transaction tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)rand.Next(),
                Script = result.Script,
                ValidUntilBlock = height + system.Settings.MaxValidUntilBlockIncrement,
                Signers = new Signer[] { new Signer() { Account = wallet.GetAccounts().ToArray()[0].ScriptHash, Scopes = WitnessScope.Global } },
                Attributes = System.Array.Empty<TransactionAttribute>(),
            };
            tx.SystemFee = result.GasConsumed + fee;
            tx.NetworkFee = wallet.CalculateNetworkFee(snapshot, tx);
            var data = new ContractParametersContext(snapshot, tx, system.Settings.Network);
            wallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            txId = tx.Hash;
            actor.Tell(tx);
            Utility.Log("client", LogLevel.Debug, string.Format("neo client invoke,method:{0},tx_hash:{1}", method, tx.Hash.ToString()));
            return true;
        }

        public InvokeResult TestInvoke(UInt160 contractHash, string method, params object[] args)
        {
            byte[] script = contractHash.MakeScript(method, args);
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            FakeSigners signers = new FakeSigners(accounts.ToArray()[0].ScriptHash);
            return GetInvokeResult(script, signers);
        }

        private InvokeResult GetInvokeResult(byte[] script, FakeSigners signers = null, bool testMode = true)
        {
            SnapshotCache snapshot = system.GetSnapshot();
            ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, container: signers, null, system.Settings, 0, 20000000000);
            return new InvokeResult() { State = engine.State, GasConsumed = (long)engine.GasConsumed, Script = script, ResultStack = engine.ResultStack.ToArray<StackItem>() };
        }

        public void TransferGas(UInt160 to, long amount)
        {
            SnapshotCache snapshotCache = system.GetSnapshot();
            UInt160 assetId = NativeContract.GAS.Hash;
            AssetDescriptor descriptor = new AssetDescriptor(snapshotCache, system.Settings, assetId);
            BigDecimal pamount = BigDecimal.Parse(amount.ToString(), descriptor.Decimals);
            Transaction tx = wallet.MakeTransaction(snapshotCache, new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = pamount,
                    ScriptHash = to
                }
            });
            if (tx == null) throw new Exception("Insufficient funds");
            ContractParametersContext data = new ContractParametersContext(snapshotCache, tx, system.Settings.Network);
            wallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            actor.Tell(tx);
        }
    }
}
