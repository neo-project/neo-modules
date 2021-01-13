using Akka.Actor;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.StateService.Storage;
using System;
using Neo.IO;

namespace Neo.Plugins.StateService
{
    public partial class StatePlugin : Plugin, IP2PPlugin
    {
        internal const string StatePayloadCategory = "StateService";
        private readonly HashSetCache<UInt256> knownHashes = new HashSetCache<UInt256>(Blockchain.Singleton.MemPool.Capacity * 2 / 5);

        bool IP2PPlugin.OnP2PMessage(Message message)
        {
            if (message.Command != MessageCommand.Extensible) return true;

            var payload = (ExtensiblePayload)message.Payload;
            if (payload.Category != StatePayloadCategory) return true;

            if (knownHashes.Contains(payload.Hash)) return false;
            knownHashes.Add(payload.Hash);

            using var snapshot = Blockchain.Singleton.GetSnapshot();
            if (!payload.Verify(snapshot)) return false;

            try
            {
                var state_root = payload.Data?.AsSerializable<StateRoot>();
                if (state_root != null)
                    return (bool)Store.Ask(state_root).Result;
            }
            catch (Exception e)
            {
                Utility.Log(nameof(StatePlugin), LogLevel.Warning, " invalid state root" + e.Message);
            }
            return false;
        }
    }
}
