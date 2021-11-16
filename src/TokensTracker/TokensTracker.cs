using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.Storage;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using static System.IO.Path;

namespace Neo.Plugins
{
    public class TokensTracker : Plugin, IPersistencePlugin
    {
        private bool _shouldTrackHistory;
        private uint _maxResults;
        private uint _network;
        private string _dbPath;
        private IStore _db;
        private NeoSystem neoSystem;
        private readonly List<TrackerBase> trackers = new();

        public override string Description => "Enquiries NEP-11 balances and transaction history of accounts through RPC";

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != _network) return;
            neoSystem = system;
            string path = string.Format(_dbPath, neoSystem.Settings.Network.ToString("X8"));
            _db = neoSystem.LoadStore(GetFullPath(path));
            trackers.Add(new Nep11Tracker(_db, _maxResults, _shouldTrackHistory, neoSystem));
            trackers.Add(new Nep17Tracker(_db, _maxResults, _shouldTrackHistory, neoSystem));
            foreach (TrackerBase tracker in trackers)
                RpcServerPlugin.RegisterMethods(tracker, _network);
        }

        protected override void Configure()
        {
            _dbPath = GetConfiguration().GetSection("DBPath").Value ?? "TokensBalanceData";
            _shouldTrackHistory = (GetConfiguration().GetSection("TrackHistory").Value ?? true.ToString()) != false.ToString();
            _maxResults = uint.Parse(GetConfiguration().GetSection("MaxResults").Value ?? "1000");
            _network = uint.Parse(GetConfiguration().GetSection("Network").Value ?? "860833102");
        }

        private void ResetBatch()
        {
            foreach (var tracker in trackers)
            {
                tracker.ResetBatch();
            }
        }

        void IPersistencePlugin.OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network != _network) return;
            // Start freshly with a new DBCache for each block.
            ResetBatch();
            foreach (var tracker in trackers)
            {
                tracker.OnPersist(system, block, snapshot, applicationExecutedList);
            }
        }

        void IPersistencePlugin.OnCommit(NeoSystem system, Block block, DataCache snapshot)
        {
            if (system.Settings.Network != _network) return;
            foreach (var tracker in trackers)
            {
                tracker.Commit();
            }
        }

        bool IPersistencePlugin.ShouldThrowExceptionFromCommit(Exception ex)
        {
            return true;
        }

    }
}
