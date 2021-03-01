using Akka.Actor;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.StateService.Network;
using Neo.Plugins.StateService.Storage;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System.Collections.Concurrent;
using System.IO;

namespace Neo.Plugins.StateService.Verification
{
    class VerificationContext
    {
        private const uint MaxValidUntilBlockIncrement = 100;
        private StateRoot root;
        private ExtensiblePayload rootPayload;
        private ExtensiblePayload votePayload;
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
        public uint RootIndex => rootIndex;
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
        public ExtensiblePayload StateRootMessage => rootPayload;
        public ExtensiblePayload VoteMessage
        {
            get
            {
                if (votePayload is null)
                    votePayload = CreateVoteMessage();
                return votePayload;
            }
        }

        public VerificationContext(Wallet wallet, uint index)
        {
            this.wallet = wallet;
            Retries = 0;
            myIndex = -1;
            rootIndex = index;
            verifiers = NativeContract.RoleManagement.GetDesignatedByRole(StatePlugin.System.StoreView, Role.StateValidator, index);
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

        private ExtensiblePayload CreateVoteMessage()
        {
            if (StateRoot is null) return null;
            if (!signatures.TryGetValue(myIndex, out byte[] sig))
            {
                sig = StateRoot.Sign(keyPair, StatePlugin.System.Settings.Magic);
                signatures[myIndex] = sig;
            }
            return CreatePayload(MessageType.Vote, new Vote
            {
                RootIndex = rootIndex,
                ValidatorIndex = myIndex,
                Signature = sig
            }, VerificationService.MaxCachedVerificationProcessCount);
        }

        public bool AddSignature(int index, byte[] sig)
        {
            if (M <= signatures.Count) return false;
            if (index < 0 || verifiers.Length <= index) return false;
            if (signatures.ContainsKey(index)) return false;
            Utility.Log(nameof(VerificationContext), LogLevel.Info, $"vote received, height={rootIndex}, index={index}");
            ECPoint validator = verifiers[index];
            byte[] hash_data = StateRoot?.GetSignData(StatePlugin.System.Settings.Magic);
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
            ContractParametersContext sc = new ContractParametersContext(StatePlugin.System.StoreView, StateRoot);
            for (int i = 0, j = 0; i < verifiers.Length && j < M; i++)
            {
                if (!signatures.TryGetValue(i, out byte[] sig)) continue;
                sc.AddSignature(contract, verifiers[i], sig);
                j++;
            }
            StateRoot.Witness = sc.GetWitnesses()[0];
            rootPayload = CreatePayload(MessageType.StateRoot, StateRoot, MaxValidUntilBlockIncrement);
            return true;
        }

        private ExtensiblePayload CreatePayload(MessageType type, ISerializable payload, uint validBlockEndThreshold)
        {
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)type);
                payload.Serialize(writer);
                writer.Flush();
                data = ms.ToArray();
            }
            ExtensiblePayload msg = new ExtensiblePayload
            {
                Category = StatePlugin.StatePayloadCategory,
                ValidBlockStart = StateRoot.Index,
                ValidBlockEnd = StateRoot.Index + validBlockEndThreshold,
                Sender = Contract.CreateSignatureRedeemScript(verifiers[MyIndex]).ToScriptHash(),
                Data = data,
            };
            ContractParametersContext sc = new ContractParametersContext(StatePlugin.System.StoreView, rootPayload);
            wallet.Sign(sc);
            msg.Witness = sc.GetWitnesses()[0];
            return msg;
        }
    }
}
