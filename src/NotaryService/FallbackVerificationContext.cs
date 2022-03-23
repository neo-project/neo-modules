using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Native;
using System.Collections.Generic;
using System.Numerics;

namespace Neo.Plugins
{
    public class FallbackVerificationContext
    {
        private readonly Dictionary<UInt160, BigInteger> signerFee = new();

        public void AddFallback(Transaction tx)
        {
            var signer = tx.Signers[1].Account;
            if (signerFee.TryGetValue(signer, out var value))
                signerFee[signer] = value + tx.SystemFee + tx.NetworkFee;
            else
                signerFee.Add(signer, tx.SystemFee + tx.NetworkFee);
        }

        public bool CheckFallback(Transaction tx, DataCache snapshot)
        {
            var signer = tx.Signers[1].Account;
            BigInteger balance = NativeContract.Notary.BalanceOf(snapshot, signer);
            signerFee.TryGetValue(signer, out var totalSenderFeeFromPool);
            BigInteger fee = tx.SystemFee + tx.NetworkFee + totalSenderFeeFromPool;
            if (balance < fee) return false;
            return true;
        }

        public void RemoveFallback(Transaction tx)
        {
            var signer = tx.Signers[1].Account;
            if ((signerFee[signer] -= tx.SystemFee + tx.NetworkFee) == 0)
                signerFee.Remove(tx.Sender);
        }
    }
}
