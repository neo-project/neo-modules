// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.StateService is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.ConsoleService;
using Neo.Cryptography.MPTTrie;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.StateService.Network;
using Neo.Plugins.StateService.Storage;
using Neo.Plugins.StateService.Verification;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins.StateService
{
    public class StatePlugin : Plugin
    {
        public const string StatePayloadCategory = "StateService";
        public override string Name => "StateService";
        public override string Description => "Enables MPT for the node";

        internal IActorRef Store;
        internal IActorRef Verifier;

        internal static NeoSystem System;
        private IWalletProvider _walletProvider;

        public StatePlugin()
        {
            Blockchain.Committing += OnCommitting;
            Blockchain.Committed += OnCommitted;
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != Settings.Default.Network) return;
            System = system;
            Store = System.ActorSystem.ActorOf(StateStore.Props(this, string.Format(Settings.Default.Path, system.Settings.Network.ToString("X8"))));
            System.ServiceAdded += NeoSystem_ServiceAdded;
            RpcServerPlugin.RegisterMethods(this, Settings.Default.Network);
        }

        private void NeoSystem_ServiceAdded(object sender, object service)
        {
            if (service is IWalletProvider provider)
            {
                _walletProvider = provider;
                System.ServiceAdded -= NeoSystem_ServiceAdded;
                if (Settings.Default.AutoVerify)
                {
                    _walletProvider.WalletChanged += WalletProvider_WalletChanged;
                }
            }
        }

        private void WalletProvider_WalletChanged(object sender, Wallet wallet)
        {
            _walletProvider.WalletChanged -= WalletProvider_WalletChanged;
            Start(wallet);
        }

        public override void Dispose()
        {
            base.Dispose();
            Blockchain.Committing -= OnCommitting;
            Blockchain.Committed -= OnCommitted;
            if (Store is not null) System.EnsureStopped(Store);
            if (Verifier is not null) System.EnsureStopped(Verifier);
        }

        private void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network != Settings.Default.Network) return;
            StateStore.Singleton.UpdateLocalStateRootSnapshot(block.Index, snapshot.GetChangeSet().Where(p => p.State != TrackState.None).Where(p => p.Key.Id != NativeContract.Ledger.Id).ToList());
        }

        private void OnCommitted(NeoSystem system, Block block)
        {
            if (system.Settings.Network != Settings.Default.Network) return;
            StateStore.Singleton.UpdateLocalStateRoot(block.Index);
        }

        [ConsoleCommand("start states", Category = "StateService", Description = "Start as a state verifier if wallet is open")]
        private void OnStartVerifyingState()
        {
            if (System is null || System.Settings.Network != Settings.Default.Network) throw new InvalidOperationException("Network doesn't match");
            Start(_walletProvider.GetWallet());
        }

        public void Start(Wallet wallet)
        {
            if (Verifier is not null)
            {
                ConsoleHelper.Warning("Already started!");
                return;
            }
            if (wallet is null)
            {
                ConsoleHelper.Warning("Please open wallet first!");
                return;
            }
            Verifier = System.ActorSystem.ActorOf(VerificationService.Props(wallet));
        }

        [ConsoleCommand("state root", Category = "StateService", Description = "Get state root by index")]
        private void OnGetStateRoot(uint index)
        {
            if (System is null || System.Settings.Network != Settings.Default.Network) throw new InvalidOperationException("Network doesn't match");
            using var snapshot = StateStore.Singleton.GetSnapshot();
            StateRoot stateRoot = snapshot.GetStateRoot(index);
            if (stateRoot is null)
                ConsoleHelper.Warning("Unknown state root");
            else
                ConsoleHelper.Info(stateRoot.ToJson().ToString());
        }

        [ConsoleCommand("state height", Category = "StateService", Description = "Get current state root index")]
        private void OnGetStateHeight()
        {
            if (System is null || System.Settings.Network != Settings.Default.Network) throw new InvalidOperationException("Network doesn't match");
            ConsoleHelper.Info("LocalRootIndex: ",
                $"{StateStore.Singleton.LocalRootIndex}",
                " ValidatedRootIndex: ",
                $"{StateStore.Singleton.ValidatedRootIndex}");
        }

        [ConsoleCommand("get proof", Category = "StateService", Description = "Get proof of key and contract hash")]
        private void OnGetProof(UInt256 rootHash, UInt160 scriptHash, string key)
        {
            if (System is null || System.Settings.Network != Settings.Default.Network) throw new InvalidOperationException("Network doesn't match");
            try
            {
                ConsoleHelper.Info("Proof: ", GetProof(rootHash, scriptHash, Convert.FromBase64String(key)));
            }
            catch (RpcException e)
            {
                ConsoleHelper.Error(e.Message);
            }
        }

        [ConsoleCommand("verify proof", Category = "StateService", Description = "Verify proof, return value if successed")]
        private void OnVerifyProof(UInt256 rootHash, string proof)
        {
            try
            {
                ConsoleHelper.Info("Verify Result: ",
                    VerifyProof(rootHash, Convert.FromBase64String(proof)));
            }
            catch (RpcException e)
            {
                ConsoleHelper.Error(e.Message);
            }
        }

        [RpcMethod]
        public JToken GetStateRoot(JArray parameters)
        {
            uint index = uint.Parse(parameters[0].AsString());
            using var snapshot = StateStore.Singleton.GetSnapshot();
            StateRoot stateRoot = snapshot.GetStateRoot(index);
            if (stateRoot is null)
                throw new RpcException(-100, "Unknown state root");
            else
                return stateRoot.ToJson();
        }

        private string GetProof(Trie trie, int contractId, byte[] key)
        {
            StorageKey sKey = new()
            {
                Id = contractId,
                Key = key,
            };
            return GetProof(trie, sKey);
        }

        private string GetProof(Trie trie, StorageKey sKey)
        {
            var result = trie.TryGetProof(sKey.ToArray(), out var proof);
            if (!result) throw new KeyNotFoundException();

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms, Utility.StrictUTF8);

            writer.WriteVarBytes(sKey.ToArray());
            writer.WriteVarInt(proof.Count);
            foreach (var item in proof)
            {
                writer.WriteVarBytes(item);
            }
            writer.Flush();

            return Convert.ToBase64String(ms.ToArray());
        }

        private string GetProof(UInt256 rootHash, UInt160 scriptHash, byte[] key)
        {
            if (!Settings.Default.FullState && StateStore.Singleton.CurrentLocalRootHash != rootHash)
            {
                throw new RpcException(-100, "Old state not supported");
            }
            using var store = StateStore.Singleton.GetStoreSnapshot();
            var trie = new Trie(store, rootHash);
            var contract = GetHistoricalContractState(trie, scriptHash);
            if (contract is null) throw new RpcException(-100, "Unknown contract");
            return GetProof(trie, contract.Id, key);
        }

        [RpcMethod]
        public JToken GetProof(JArray parameters)
        {
            UInt256 rootHash = UInt256.Parse(parameters[0].AsString());
            UInt160 scriptHash = UInt160.Parse(parameters[1].AsString());
            byte[] key = Convert.FromBase64String(parameters[2].AsString());
            return GetProof(rootHash, scriptHash, key);
        }

        private string VerifyProof(UInt256 rootHash, byte[] proof)
        {
            var proofs = new HashSet<byte[]>();

            using MemoryStream ms = new(proof, false);
            using BinaryReader reader = new(ms, Utility.StrictUTF8);

            var key = reader.ReadVarBytes(Node.MaxKeyLength);
            var count = reader.ReadVarInt();
            for (ulong i = 0; i < count; i++)
            {
                proofs.Add(reader.ReadVarBytes());
            }

            var value = Trie.VerifyProof(rootHash, key, proofs);
            if (value is null) throw new RpcException(-100, "Verification failed");
            return Convert.ToBase64String(value);
        }

        [RpcMethod]
        public JToken VerifyProof(JArray parameters)
        {
            UInt256 rootHash = UInt256.Parse(parameters[0].AsString());
            byte[] proofBytes = Convert.FromBase64String(parameters[1].AsString());
            return VerifyProof(rootHash, proofBytes);
        }

        [RpcMethod]
        public JToken GetStateHeight(JArray parameters)
        {
            return new JObject
            {
                ["localrootindex"] = StateStore.Singleton.LocalRootIndex,
                ["validatedrootindex"] = StateStore.Singleton.ValidatedRootIndex
            };
        }

        private ContractState GetHistoricalContractState(Trie trie, UInt160 scriptHash)
        {
            const byte prefix = 8;
            StorageKey sKey = new KeyBuilder(NativeContract.ContractManagement.Id, prefix).Add(scriptHash);
            return trie.TryGetValue(sKey.ToArray(), out var value) ? value.AsSerializable<StorageItem>().GetInteroperable<ContractState>() : null;
        }

        private StorageKey ParseStorageKey(byte[] data)
        {
            return new()
            {
                Id = BinaryPrimitives.ReadInt32LittleEndian(data),
                Key = data.AsMemory(sizeof(int)),
            };
        }

        [RpcMethod]
        public JToken FindStates(JArray parameters)
        {
            var rootHash = UInt256.Parse(parameters[0].AsString());
            if (!Settings.Default.FullState && StateStore.Singleton.CurrentLocalRootHash != rootHash)
                throw new RpcException(-100, "Old state not supported");
            var scriptHash = UInt160.Parse(parameters[1].AsString());
            var prefix = Convert.FromBase64String(parameters[2].AsString());
            byte[] key = Array.Empty<byte>();
            if (3 < parameters.Count)
                key = Convert.FromBase64String(parameters[3].AsString());
            int count = Settings.Default.MaxFindResultItems;
            if (4 < parameters.Count)
                count = int.Parse(parameters[4].AsString());
            if (Settings.Default.MaxFindResultItems < count)
                count = Settings.Default.MaxFindResultItems;
            using var store = StateStore.Singleton.GetStoreSnapshot();
            var trie = new Trie(store, rootHash);
            var contract = GetHistoricalContractState(trie, scriptHash);
            if (contract is null) throw new RpcException(-100, "Unknown contract");
            StorageKey pKey = new()
            {
                Id = contract.Id,
                Key = prefix,
            };
            StorageKey fKey = new()
            {
                Id = pKey.Id,
                Key = key,
            };
            JObject json = new();
            JArray jArr = new();
            int i = 0;
            foreach (var (ikey, ivalue) in trie.Find(pKey.ToArray(), 0 < key.Length ? fKey.ToArray() : null))
            {
                if (count < i) break;
                if (i < count)
                {
                    JObject j = new();
                    j["key"] = Convert.ToBase64String(ParseStorageKey(ikey.ToArray()).Key.Span);
                    j["value"] = Convert.ToBase64String(ivalue.Span);
                    jArr.Add(j);
                }
                i++;
            };
            if (0 < jArr.Count)
            {
                json["firstProof"] = GetProof(trie, contract.Id, Convert.FromBase64String(jArr.First()["key"].AsString()));
            }
            if (1 < jArr.Count)
            {
                json["lastProof"] = GetProof(trie, contract.Id, Convert.FromBase64String(jArr.Last()["key"].AsString()));
            }
            json["truncated"] = count < i;
            json["results"] = jArr;
            return json;
        }

        [RpcMethod]
        public JToken GetState(JArray parameters)
        {
            var rootHash = UInt256.Parse(parameters[0].AsString());
            if (!Settings.Default.FullState && StateStore.Singleton.CurrentLocalRootHash != rootHash)
                throw new RpcException(-100, "Old state not supported");
            var scriptHash = UInt160.Parse(parameters[1].AsString());
            var key = Convert.FromBase64String(parameters[2].AsString());
            using var store = StateStore.Singleton.GetStoreSnapshot();
            var trie = new Trie(store, rootHash);

            var contract = GetHistoricalContractState(trie, scriptHash);
            if (contract is null) throw new RpcException(-100, "Unknown contract");
            StorageKey sKey = new()
            {
                Id = contract.Id,
                Key = key,
            };
            return Convert.ToBase64String(trie[sKey.ToArray()]);
        }
    }
}
