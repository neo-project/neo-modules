using Akka.Actor;
using Neo.IO.Caching;
using static Neo.Ledger.Blockchain;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.StateService.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins.StateService
{
    public partial class StatePlugin : Plugin, IPersistencePlugin
    {
        void IPersistencePlugin.OnPersist(Block block, StoreView snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            List<StateStore.Item> changes = new List<StateStore.Item>();
            foreach (var item in snapshot.Storages.GetChangeSet().Where(p => p.State != TrackState.None))
            {
                changes.Add(new StateStore.Item
                {
                    State = item.State,
                    Key = item.Key,
                    Value = item.Item,
                });
            }
            Store.Tell(new StateStore.StorageChanges
            {
                Height = block.Index,
                ChangeSet = changes,
            });
        }

        bool IPersistencePlugin.ShouldThrowExceptionFromCommit(Exception ex) => false;

        void IPersistencePlugin.OnCommit(Block block, StoreView snapshot) { }
    }
}
