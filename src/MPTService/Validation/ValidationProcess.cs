using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.MPTService.MPTStorage;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.SmartContract.Native.Designate;
using Neo.Wallets;
using System;
using System.Collections.Generic;

namespace Neo.Plugins.MPTService.Validation
{
    public class ValidationProcess
    {
        public StateRoot root;
        public readonly Wallet wallet;
        public KeyPair keyPair;
        public int MyIndex;
        public uint RootIndex;
        public ECPoint[] Validators;
        public int M => Validators.Length - (Validators.Length - 1) / 3;
        public bool VoteSent => Signatures.ContainsKey(MyIndex);
        public Dictionary<int, byte[]> Signatures = new Dictionary<int, byte[]>();
        public bool IsValidator => MyIndex >= 0;

        public StateRoot StateRoot
        {
            get
            {
                if (root is null)
                    root = MPTStore.Singleton.StateRoots.TryGet(RootIndex);
                return root;
            }
        }

        public ValidationProcess() { }

        public ValidationProcess(Wallet wallet, uint index)
        {
            this.wallet = wallet;
            Initialize(index);
        }

        public void Initialize(uint index)
        {
            MyIndex = -1;
            root = null;
            RootIndex = index;
            using SnapshotView snapshot = Blockchain.Singleton.GetSnapshot();
            Validators = NativeContract.Designate.GetDesignatedByRole(snapshot, Role.StateValidator, index);
            for (int i = 0; i < Validators.Length; i++)
            {
                WalletAccount account = wallet?.GetAccount(Validators[i]);
                if (account?.HasKey != true) continue;
                MyIndex = i;
                keyPair = account.GetKey();
                break;
            }
        }

        public Vote CreateVote()
        {
            if (StateRoot is null) return null;
            if (!Signatures.TryGetValue(MyIndex, out byte[] sig))
            {
                sig = StateRoot.Sign(keyPair);
                Signatures[MyIndex] = sig;
            }
            return new Vote(RootIndex, MyIndex, sig);
        }

        public bool AddSignature(int index, byte[] sig)
        {
            if (M <= Signatures.Count) return false;
            if (index < 0 || Validators.Length <= index) return false;
            if (Signatures.ContainsKey(index)) return false;
            Utility.Log(nameof(ValidationService), LogLevel.Info, $"vote received, index={index}");
            ECPoint validator = Validators[index];
            byte[] hash_data = StateRoot?.GetHashData();
            if (hash_data != null && !Crypto.VerifySignature(hash_data, sig, validator))
            {
                Utility.Log(nameof(ValidationService), LogLevel.Info, "incorrect vote, invalid signature");
                return false;
            }
            Signatures.Add(index, sig);
            return true;
        }

        public void CreateStateRoot()
        {
            Contract contract = Contract.CreateMultiSigContract(M, Validators);
            ContractParametersContext sc = new ContractParametersContext(StateRoot);
            for (int i = 0, j = 0; i < Validators.Length && j < M; i++)
            {
                bool ok = Signatures.TryGetValue(i, out byte[] sig);
                if (!ok) continue;
                sc.AddSignature(contract, Validators[i], sig);
                j++;
            }
            StateRoot.Witness = sc.GetWitnesses()[0];
        }

        public bool CheckSignatures()
        {
            if (StateRoot is null) return false;
            if (Signatures.Count < M) return false;
            if (StateRoot.Witness != null) return true;
            CreateStateRoot();
            return true;
        }
    }
}
