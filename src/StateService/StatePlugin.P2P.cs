using Akka.Actor;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.StateService.Storage;
using System;
using Neo.IO;

namespace Neo.Plugins.StateService
{
    public partial class StatePlugin : Plugin, IP2PPlugin
    {
        public const string StatePayloadCategory = "StateService";

        void IP2PPlugin.OnVerifiedInventory(IInventory inventory)
        {
            if (inventory is ExtensiblePayload payload)
            {
                if (payload.Category != StatePayloadCategory) return;
                try
                {
                    var state_root = payload.Data?.AsSerializable<StateRoot>();
                    if (state_root != null) Store.Tell(state_root);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(StatePlugin), LogLevel.Warning, " invalid state root" + e.Message);
                }
            }
        }
    }
}
