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
        Wallet Wallet { get; }
        NeoSystem NeoSystem { get; }
        IActorRef Blockchain { get; }

        protected void Invoke(UInt160 contractHash, string method, long fee, params object[] args)
        {
            InvokeResult result = TestInvoke(contractHash, method, args);
            if (result.State != VMState.HALT) throw new InvalidOperationException($"perform invoke failed, state={result.State}");
            using SnapshotCache snapshot = NeoSystem.GetSnapshot();
            uint height = NativeContract.Ledger.CurrentIndex(snapshot);
            Random rand = new();
            Transaction tx = new()
            {
                Version = 0,
                Nonce = (uint)rand.Next(),
                Script = result.Script,
                ValidUntilBlock = height + NeoSystem.Settings.MaxValidUntilBlockIncrement,
                Signers = new Signer[] { new Signer() { Account = Wallet.GetAccounts().ToArray()[0].ScriptHash, Scopes = WitnessScope.Global } },
                Attributes = Array.Empty<TransactionAttribute>(),
            };
            tx.SystemFee = result.GasConsumed + fee;
            tx.NetworkFee = Wallet.CalculateNetworkFee(snapshot, tx);
            var balance = NativeContract.GAS.BalanceOf(snapshot, Wallet.GetAccounts().First().ScriptHash);
            if (tx.SystemFee + tx.NetworkFee > balance)
            {
                Utility.Log(nameof(Invoker), LogLevel.Debug, $"insufficient gas, network={NeoSystem.Settings.Network}, method={method}, height={height}, system_fee={tx.SystemFee}, network_fee={tx.NetworkFee}, balance={balance}");
                throw new InvalidOperationException($"make transaction failed, gas insufficient");
            }
            var data = new ContractParametersContext(snapshot, tx, NeoSystem.Settings.Network);
            if (!Wallet.Sign(data)) throw new InvalidOperationException($"make transaction failed, wallet could not sign");
            tx.Witnesses = data.GetWitnesses();
            Blockchain.Tell(tx);
            Utility.Log(nameof(Invoker), LogLevel.Debug, $"tx sent, network={NeoSystem.Settings.Network}, tid={tx.Hash}, height={height}, method={method}");
        }

        protected InvokeResult TestInvoke(UInt160 contractHash, string method, params object[] args)
        {
            byte[] script = contractHash.MakeScript(method, args);
            FakeSigners signers = new(Wallet.GetAccounts().First().ScriptHash);
            return GetInvokeResult(script, signers);
        }

        protected InvokeResult GetInvokeResult(byte[] script, FakeSigners signers = null)
        {
            using SnapshotCache snapshot = NeoSystem.GetSnapshot();
            ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, container: signers, null, NeoSystem.Settings, 0, 20000000000);
            return new InvokeResult() { State = engine.State, GasConsumed = engine.GasConsumed, Script = script, ResultStack = engine.ResultStack.ToArray() };
        }

        public void TransferGas(UInt160 to, long amount)
        {
            var account = Wallet.GetAccounts().ToArray()[0].ScriptHash;
            Invoke(NativeContract.GAS.Hash, "transfer", 0, account, to, amount, Array.Empty<byte>());
            Utility.Log(nameof(Invoker), LogLevel.Debug, $"gas sent, network={NeoSystem.Settings.Network}, to={to}, amount={amount}");
        }

        public long GasBalance()
        {
            using SnapshotCache snapshot = NeoSystem.GetSnapshot();
            var account = Wallet.GetAccounts().ToArray()[0].ScriptHash;
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

        private int KeyPosition(ECPoint key, ECPoint[] list)
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
