using Akka.Actor;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.StateService.Storage;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System.Collections.Concurrent;

namespace Neo.Plugins.StateService.Verification
{
    public class VerificationContext
    {
        private const uint MaxValidUntilBlockIncrement = 100;
        private StateRoot root;
        private ExtensiblePayload payload;
        private readonly Wallet wallet;
        private readonly KeyPair keyPair;
        private readonly int myIndex;
        private readonly uint rootIndex;
        private readonly ECPoint[] verifiers;
        private int M => verifiers.Length - (verifiers.Length - 1) / 3;
        private readonly ConcurrentDictionary<int, byte[]> signatures = new ConcurrentDictionary<int, byte[]>();

        public int Retries;
        public bool IsValidator => myIndex >= 0;
        public int MyIndex => myIndex;
        public ECPoint[] Verifiers => verifiers;
        public ICancelable Timer;
        public StateRoot StateRoot
        {
            get
            {
                if (root is null)
                {
                    using var snapshot = StateStore.Singleton.GetSnapshot();
                    root = snapshot.GetStateRoot(rootIndex);
                }
                return root;
            }
        }
        public ExtensiblePayload Message => payload;

        public VerificationContext(Wallet wallet, uint index)
        {
            this.wallet = wallet;
            Retries = 0;
            myIndex = -1;
            root = null;
            rootIndex = index;
            using SnapshotCache snapshot = Blockchain.Singleton.GetSnapshot();
            verifiers = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.StateValidator, index);
            if (wallet is null) return;
            for (int i = 0; i < verifiers.Length; i++)
            {
                WalletAccount account = wallet.GetAccount(verifiers[i]);
                if (account?.HasKey != true) continue;
                myIndex = i;
                keyPair = account.GetKey();
                break;
            }
        }

        public Vote CreateVote()
        {
            if (StateRoot is null) return null;
            if (!signatures.TryGetValue(myIndex, out byte[] sig))
            {
                sig = StateRoot.Sign(keyPair);
                signatures[myIndex] = sig;
            }
            return new Vote(rootIndex, myIndex, sig);
        }

        public bool AddSignature(int index, byte[] sig)
        {
            if (M <= signatures.Count) return false;
            if (index < 0 || verifiers.Length <= index) return false;
            if (signatures.ContainsKey(index)) return false;
            Utility.Log(nameof(VerificationContext), LogLevel.Info, $"vote received, height={rootIndex}, index={index}");
            ECPoint validator = verifiers[index];
            byte[] hash_data = StateRoot?.GetHashData();
            if (hash_data is null || !Crypto.VerifySignature(hash_data, sig, validator))
            {
                Utility.Log(nameof(VerificationContext), LogLevel.Info, "incorrect vote, invalid signature");
                return false;
            }
            return signatures.TryAdd(index, sig);
        }

        public bool CheckSignatures()
        {
            if (StateRoot is null) return false;
            if (signatures.Count < M) return false;
            if (StateRoot.Witness != null) return true;
            Contract contract = Contract.CreateMultiSigContract(M, verifiers);
            ContractParametersContext sc = new ContractParametersContext(StateRoot);
            for (int i = 0, j = 0; i < verifiers.Length && j < M; i++)
            {
                if (!signatures.TryGetValue(i, out byte[] sig)) continue;
                sc.AddSignature(contract, verifiers[i], sig);
                j++;
            }
            StateRoot.Witness = sc.GetWitnesses()[0];

            payload = new ExtensiblePayload
            {
                Category = StatePlugin.StatePayloadCategory,
                ValidBlockStart = StateRoot.Index,
                ValidBlockEnd = StateRoot.Index + MaxValidUntilBlockIncrement,
                Sender = Contract.CreateSignatureRedeemScript(verifiers[MyIndex]).ToScriptHash(),
                Data = StateRoot.ToArray(),
            };
            sc = new ContractParametersContext(payload);
            wallet.Sign(sc);
            payload.Witness = sc.GetWitnesses()[0];
            return true;
        }
    }
}
