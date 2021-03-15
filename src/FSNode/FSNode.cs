using Akka.Actor;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.FSStorage.innerring;
using Neo.Plugins.util;
using Neo.SmartContract;
using Neo.VM;
using System.Collections.Generic;
using static Neo.Plugins.FSStorage.innerring.InnerRingService;

namespace Neo.Plugins.FSStorage
{
    /// <summary>
    /// The entrance of the Fs program.
    /// Built-in an innering service to process notification events related to FS when the block is persisted.
    /// </summary>
    public class FSNode : Plugin, IPersistencePlugin
    {
        public override string Name => "FSNode";
        public override string Description => "Uses FSNode to provide distributed file storage service";
        public IActorRef innering;

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnPluginsLoaded()
        {
            base.OnPluginsLoaded();
            if (Settings.Default.IsSender)
            {
                innering = System.ActorSystem.ActorOf(InnerRingSender.Props());
                return;
            }
            innering = System.ActorSystem.ActorOf(InnerRingService.Props(Plugin.System));
            RpcServerPlugin.RegisterMethods(this, Settings.Default.Network);
            innering.Tell(new Start() { });
        }

        public void OnPersist(Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList) {
            foreach (var appExec in applicationExecutedList)
            {
                Transaction tx = appExec.Transaction;
                VMState state = appExec.VMState;
                if (tx is null || state != VMState.HALT) continue;
                var notifys = appExec.Notifications;
                if (notifys is null) continue;
                foreach (var notify in notifys)
                {
                    var contract = notify.ScriptHash;
                    if (Settings.Default.IsSender)
                    {
                        if (contract != Settings.Default.FsContractHash) continue;
                        innering.Tell(new InnerRingSender.MainContractEvent() { notify = notify });
                    }
                    else
                    {
                        if (!Settings.Default.Contracts.Contains(contract)) continue;
                        innering.Tell(new MorphContractEvent() { notify = notify });
                    }
                }
            }
        }

        [RpcMethod]
        public JObject ReceiveMainNetEvent(JArray _params)
        {
            var notify = GetNotifyEventArgsFromJson(_params);
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("contracthash", notify.ScriptHash.ToString());
            pairs.Add("eventname", notify.EventName);
            pairs.Add("state", notify.State.ToJson().ToString());
            Neo.Utility.Log("NeoFS Rpc", LogLevel.Info, pairs.ParseToString());
            innering.Tell(new MainContractEvent() { notify = notify });
            return true;
        }

        public static NotifyEventArgs GetNotifyEventArgsFromJson(JArray _params)
        {
            IVerifiable container = _params[0].AsString().HexToBytes().AsSerializable<Transaction>();
            UInt160 contractHash = UInt160.Parse(_params[1].AsString().HexToBytes().ToHexString(true));
            string eventName = _params[2].AsString();
            IEnumerator<JObject> array = ((JArray)_params[3]).GetEnumerator();
            VM.Types.Array state = new VM.Types.Array();
            while (array.MoveNext())
            {
                state.Add(Network.RPC.Utility.StackItemFromJson(array.Current));
            }
            return new NotifyEventArgs(container, contractHash, eventName, state);
        }

        public override void Dispose()
        {
            base.Dispose();
            innering.Tell(new Stop() { });
        }
    }
}
