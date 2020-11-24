using Neo.ConsoleService;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.MPTService.MPTStorage;
using System;

namespace Neo.Plugins.MPTService
{
    public partial class MPTPlugin : Plugin, IPersistencePlugin
    {
        [ConsoleCommand("state root", Category = "MPT", Description = "Get state root by index")]
        private void OnGetStateRoot(uint index)
        {
            StateRoot state_root = MPTStore.Singleton.StateRoots.TryGet(index);
            if (state_root is null)
                Console.WriteLine("Unknown state root");
            else
                Console.WriteLine(state_root.ToJson());
        }

        [ConsoleCommand("current root", Category = "MPT", Description = "Get state root by index")]
        private void OnGetCurrentRootHash()
        {
            Console.WriteLine(MPTStore.Singleton.CurrentLocalRootHash);
        }

        [ConsoleCommand("state height", Category = "MPT", Description = "Get current state root index")]
        private void OnGetStateHeight()
        {
            Console.WriteLine($"LocalRootIndex: {MPTStore.Singleton.LocalRootIndex}, ValidatedRootIndex: {MPTStore.Singleton.ValidatedRootIndex}");
        }
    }
}
