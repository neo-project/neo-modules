using System.Linq;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM.Types;
using Neo.Wallets;

namespace Neo.FileStorage.Morph.Invoker
{
    public class Client : IClient
    {
        public IClient client;

        public Wallet GetWallet()
        {
            return client.GetWallet();
        }

        public bool Invoke(out UInt256 txId, UInt160 contractHash, string method, long fee, params object[] args)
        {
            return client.Invoke(out txId, contractHash, method, fee, args);
        }

        public InvokeResult TestInvoke(UInt160 contractHash, string method, params object[] args)
        {
            return client.TestInvoke(contractHash, method, args);
        }

        public bool TransferGas(UInt160 receiver, long amount)
        {
            var account = GetWallet().GetAccounts().ToArray()[0].GetKey().PublicKey.EncodePoint(true).ToScriptHash();
            var result = client.Invoke(out var txId, NativeContract.GAS.Hash, "transfer", 0, account, receiver, amount, new byte[0]);
            Utility.Log("", LogLevel.Debug, string.Format("native gas transfer invoke,to:{0},tx_hash:{1}", receiver.ToString(), txId.ToString()));
            return result;
        }

        public long GasBalance()
        {
            var result = client.TestInvoke(NativeContract.GAS.Hash, "balanceOf");
            return (long)result.ResultStack[0].GetInteger();
        }
        public ECPoint[] Committee()
        {
            var result = client.TestInvoke(NativeContract.NEO.Hash, "getCommittee");
            return ((Array)result.ResultStack[0]).Select(p => p.GetSpan().AsSerializable<ECPoint>()).ToArray();
        }

        public ECPoint[] NeoFSAlphabetList()
        {
            var result = client.TestInvoke(NativeContract.NEO.Hash, "getCommittee");
            return ((Array)result.ResultStack[0]).Select(p => p.GetSpan().AsSerializable<ECPoint>()).ToArray();
        }

    }
}
