using Akka.Actor;
using Neo.IO.Caching;
using static Neo.Ledger.Blockchain;
using Neo.Persistence;
using Neo.Plugins.MPTService.MPTStorage;
using Neo.Plugins.MPTService.Validation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins.MPTService
{
    public partial class MPTPlugin : Plugin, IPersistencePlugin
    {
        public void OnPersist(StoreView snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            List<MPTStore.Item> changes = new List<MPTStore.Item>();
            foreach (var item in snapshot.Storages.GetChangeSet().Where(p => p.State != TrackState.None))
            {
                changes.Add(new MPTStore.Item
                {
                    State = item.State,
                    Key = item.Key,
                    Value = item.Item,
                });
            }
            Store.Tell(new MPTStore.StorageChanges
            {
                Height = snapshot.PersistingBlock.Index,
                ChangeSet = changes,
            });
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex) => false;

        public void OnCommit(StoreView snapshot) { }
    }
}
