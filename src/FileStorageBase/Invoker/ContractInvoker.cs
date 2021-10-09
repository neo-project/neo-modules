using System;
using System.Linq;
using Akka.Actor;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace Neo.FileStorage.Invoker
{
    public abstract class ContractInvoker
    {
        public Wallet Wallet { get; init; }
        public NeoSystem NeoSystem { get; init; }
        public IActorRef Blockchain { get; init; }
        private WalletAccount account;
        protected WalletAccount DefaultAccount
        {
            get
            {
                if (account is null)
                {
                    foreach (WalletAccount wa in Wallet.GetAccounts())
                    {
                        if (wa.IsDefault)
                        {
                            account = wa;
                            break;
                        }
                        if (account == null) account = wa;
                    }
                }
                return account;
            }
        }

        protected void Invoke(UInt160 contractHash, string method, long fee, params object[] args)
        {
            InvokeResult result = TestInvoke(contractHash, method, args);
            using SnapshotCache snapshot = NeoSystem.GetSnapshot();
            uint height = NativeContract.Ledger.CurrentIndex(snapshot);
            Random rand = new();
            Transaction tx = new()
            {
                Version = 0,
                Nonce = (uint)rand.Next(),
                Script = result.Script,
                ValidUntilBlock = height + NeoSystem.Settings.MaxValidUntilBlockIncrement,
                Signers = new Signer[] { new Signer() { Account = DefaultAccount.ScriptHash, Scopes = WitnessScope.Global } },
                Attributes = Array.Empty<TransactionAttribute>(),
            };
            tx.SystemFee = result.GasConsumed + fee;
            tx.NetworkFee = Wallet.CalculateNetworkFee(snapshot, tx);
            var balance = NativeContract.GAS.BalanceOf(snapshot, DefaultAccount.ScriptHash);
            if (tx.SystemFee + tx.NetworkFee > balance)
            {
                Utility.Log(nameof(ContractInvoker), LogLevel.Debug, $"insufficient gas, network={NeoSystem.Settings.Network}, method={method}, height={height}, system_fee={tx.SystemFee}, network_fee={tx.NetworkFee}, balance={balance}");
                throw new InvalidOperationException($"make transaction failed, gas insufficient");
            }
            var data = new ContractParametersContext(snapshot, tx, NeoSystem.Settings.Network);
            if (!Wallet.Sign(data)) throw new InvalidOperationException($"make transaction failed, wallet could not sign");
            tx.Witnesses = data.GetWitnesses();
            Blockchain.Tell(tx);
            Utility.Log(nameof(ContractInvoker), LogLevel.Debug, $"tx sent, network={NeoSystem.Settings.Network}, tid={tx.Hash}, height={height}, method={method}");
        }

        protected InvokeResult TestInvoke(UInt160 contractHash, string method, params object[] args)
        {
            byte[] script = contractHash.MakeScript(method, args);
            FakeSigners signers = new(DefaultAccount.ScriptHash);
            var result = GetInvokeResult(script, signers);
            if (result.State != VMState.HALT)
            {
                Utility.Log(nameof(ContractInvoker), LogLevel.Error, $"perform invoke failed, method={method}, error={result.FaultException.Message}");
                throw new InvalidOperationException($"perform invoke failed, method={method}");
            }
            return result;
        }

        protected InvokeResult GetInvokeResult(byte[] script, FakeSigners signers = null)
        {
            using SnapshotCache snapshot = NeoSystem.GetSnapshot();
            ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, container: signers, null, NeoSystem.Settings, 0, 2000000000);
            return new InvokeResult()
            {
                State = engine.State,
                GasConsumed = engine.GasConsumed,
                Script = script,
                FaultException = engine.FaultException,
                UncaughtException = engine.UncaughtException,
                ResultStack = engine.ResultStack.ToArray()
            };
        }

        public void TransferGas(UInt160 to, long amount)
        {
            var account = DefaultAccount.ScriptHash;
            Invoke(NativeContract.GAS.Hash, "transfer", 0, account, to, amount, Array.Empty<byte>());
            Utility.Log(nameof(ContractInvoker), LogLevel.Debug, $"gas sent, network={NeoSystem.Settings.Network}, to={to}, amount={amount}");
        }

        public long GasBalance()
        {
            using SnapshotCache snapshot = NeoSystem.GetSnapshot();
            var account = DefaultAccount.ScriptHash;
            return (long)NativeContract.GAS.BalanceOf(snapshot, account);
        }

        public ECPoint[] Committee()
        {
            using SnapshotCache snapshot = NeoSystem.GetSnapshot();
            return NativeContract.NEO.GetCommittee(snapshot);
        }

        public ECPoint[] NeoFSAlphabetList()
        {
            using SnapshotCache snapshot = NeoSystem.GetSnapshot();
            var height = NativeContract.Ledger.CurrentIndex(snapshot);
            return NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.NeoFSAlphabetNode, height);
        }

        public void InnerRingIndex(ECPoint key, out int index, out int length)
        {
            ECPoint[] innerRing = NeoFSAlphabetList();
            index = KeyPosition(key, innerRing);
            length = innerRing.Length;
        }

        public int AlphabetIndex(ECPoint key)
        {
            return KeyPosition(key, Committee());
        }

        private static int KeyPosition(ECPoint key, ECPoint[] list)
        {
            var result = -1;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(key))
                {
                    result = i;
                    break;
                }
            }
            return result;
        }
    }
}
