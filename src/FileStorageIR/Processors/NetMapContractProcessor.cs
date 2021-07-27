using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Google.Protobuf.Collections;
using Neo.Cryptography.ECC;
using Neo.FileStorage.InnerRing.Events;
using Neo.FileStorage.InnerRing.Timer;
using Neo.FileStorage.InnerRing.Utils.Locode;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event;
using Neo.FileStorage.Listen.Event.Morph;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class NetMapContractProcessor : BaseProcessor
    {
        public override string Name => "NetMapContractProcessor";
        private const string NewEpochNotification = "NewEpoch";
        private const string AddPeerNotification = "AddPeer";
        private const string UpdatePeerStateNotification = "UpdateState";

        public CleanupTable NetmapSnapshot;
        public LocodeValidator NodeValidator;
        public Action<ContractEvent> HandleNewAudit;
        public Action<ContractEvent> HandleAuditSettlements;
        public Action<ContractEvent> HandleAlphabetSync;

        public override HandlerInfo[] ListenerHandlers()
        {
            HandlerInfo newEpochHandler = new();
            newEpochHandler.ScriptHashWithType = new ScriptHashWithType() { Type = NewEpochNotification, ScriptHashValue = NetmapContractHash };
            newEpochHandler.Handler = HandleNewEpoch;
            HandlerInfo addPeerHandler = new();
            addPeerHandler.ScriptHashWithType = new ScriptHashWithType() { Type = AddPeerNotification, ScriptHashValue = NetmapContractHash };
            addPeerHandler.Handler = HandleAddPeer;
            HandlerInfo updatePeerStateHandler = new();
            updatePeerStateHandler.ScriptHashWithType = new ScriptHashWithType() { Type = UpdatePeerStateNotification, ScriptHashValue = NetmapContractHash };
            updatePeerStateHandler.Handler = HandleUpdateState;
            return new HandlerInfo[] { newEpochHandler, addPeerHandler, updatePeerStateHandler };
        }

        public override ParserInfo[] ListenerParsers()
        {
            ParserInfo newEpochParser = new();
            newEpochParser.ScriptHashWithType = new ScriptHashWithType() { Type = NewEpochNotification, ScriptHashValue = NetmapContractHash };
            newEpochParser.Parser = NewEpochEvent.ParseNewEpochEvent;
            ParserInfo addPeerParser = new();
            addPeerParser.ScriptHashWithType = new ScriptHashWithType() { Type = AddPeerNotification, ScriptHashValue = NetmapContractHash };
            addPeerParser.Parser = AddPeerEvent.ParseAddPeerEvent;
            ParserInfo updatePeerParser = new();
            updatePeerParser.ScriptHashWithType = new ScriptHashWithType() { Type = UpdatePeerStateNotification, ScriptHashValue = NetmapContractHash };
            updatePeerParser.Parser = UpdatePeerEvent.ParseUpdatePeerEvent;
            return new ParserInfo[] { newEpochParser, addPeerParser, updatePeerParser };
        }

        public void HandleNewEpochTick()
        {
            Utility.Log(Name, LogLevel.Info, "event, type=epoch");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessNewEpochTick()) });
        }

        public void HandleNewEpoch(ContractEvent morphEvent)
        {
            NewEpochEvent newEpochEvent = (NewEpochEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, $"event, type=new_epoch, value={newEpochEvent.EpochNumber}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessNewEpoch(newEpochEvent)) });
        }

        public void HandleAddPeer(ContractEvent morphEvent)
        {
            AddPeerEvent addPeerEvent = (AddPeerEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, "event, type=add_peer");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessAddPeer(addPeerEvent)) });
        }

        public void HandleUpdateState(ContractEvent morphEvent)
        {
            UpdatePeerEvent updateStateEvent = (UpdatePeerEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, $"event, type=update_peer_state, key={updateStateEvent.PublicKey.EncodePoint(true).ToHexString()}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessUpdateState(updateStateEvent)) });
        }

        public void HandleCleanupTick(ContractEvent morphEvent)
        {
            if (!NetmapSnapshot.Enabled)
            {
                Utility.Log(Name, LogLevel.Debug, "netmap clean up routine is disabled");
                return;
            }
            NetmapCleanupTickEvent netmapCleanupTickEvent = (NetmapCleanupTickEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, "event: type=netmap_cleaner");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessNetmapCleanupTick(netmapCleanupTickEvent)) });
        }

        public void ProcessNetmapCleanupTick(NetmapCleanupTickEvent netmapCleanupTickEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore new netmap cleanup tick");
                return;
            }
            try
            {
                NetmapSnapshot.ForEachRemoveCandidate(netmapCleanupTickEvent.Epoch, (string s) =>
                {
                    ECPoint key = null;
                    try
                    {
                        key = ECPoint.FromBytes(s.HexToBytes(), ECCurve.Secp256r1);
                    }
                    catch
                    {
                        Utility.Log(Name, LogLevel.Warning, $"can't decode public key of netmap node, key={s}");
                        return;
                    }
                    Utility.Log(Name, LogLevel.Info, $"vote to remove node from netmap, key={s}");
                    try
                    {
                        MorphInvoker.UpdatePeerState(API.Netmap.NodeInfo.Types.State.Offline, key.EncodePoint(true));
                    }
                    catch (Exception e)
                    {
                        Utility.Log(Name, LogLevel.Error, $"can't invoke netmap.UpdateState, error={e}");
                    }
                });
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Warning, $"can't iterate on netmap cleaner cache, error={e}");
            }
        }

        public void ProcessNewEpochTick()
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore new epoch tick");
                return;
            }
            ulong nextEpoch = State.EpochCounter() + 1;
            Utility.Log(Name, LogLevel.Info, $"next epoch, {nextEpoch}");
            try
            {
                MorphInvoker.NewEpoch(nextEpoch);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't invoke netmap.NewEpoch, error={e}");
            }
        }

        public void ProcessNewEpoch(NewEpochEvent newEpochEvent)
        {
            State.SetEpochCounter(newEpochEvent.EpochNumber);
            State.ResetEpochTimer();
            API.Netmap.NodeInfo[] snapshot;
            try
            {
                snapshot = MorphInvoker.NetMap();
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Info, $"can't get netmap snapshot to perform cleanup, error={e}");
                return;
            }
            if (newEpochEvent.EpochNumber > 0)
            {
                try
                {
                    MorphInvoker.StartEstimation((long)newEpochEvent.EpochNumber);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, $"can't start container size estimation, epoch={newEpochEvent.EpochNumber}, error={e}");
                }
            }
            NetmapSnapshot.Update(snapshot, newEpochEvent.EpochNumber);
            HandleCleanupTick(new NetmapCleanupTickEvent() { Epoch = newEpochEvent.EpochNumber });
            HandleNewAudit(new StartEvent() { Epoch = newEpochEvent.EpochNumber });
            HandleAuditSettlements(new AuditStartEvent() { Epoch = newEpochEvent.EpochNumber });
            HandleAlphabetSync(new SyncEvent());
        }

        public void ProcessAddPeer(AddPeerEvent addPeerEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore new peer notification");
                return;
            }
            API.Netmap.NodeInfo nodeInfo = null;
            try
            {
                nodeInfo = API.Netmap.NodeInfo.Parser.ParseFrom(addPeerEvent.Node);
            }
            catch
            {
                Utility.Log(Name, LogLevel.Warning, "can't parse network map candidate");
                return;
            }
            try
            {
                NodeValidator.VerifyAndUpdate(nodeInfo);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Warning, $"could not verify and update information about network map candidate, error={e}");
                return;
            }
            RepeatedField<API.Netmap.NodeInfo.Types.Attribute> attributes = nodeInfo.Attributes;
            List<API.Netmap.NodeInfo.Types.Attribute> attr = attributes.ToList();
            attr.Sort((x, y) =>
            {
                var compareResult = x.Key.CompareTo(y.Key);
                if (compareResult != 0) return compareResult;
                else return x.Value.CompareTo(y.Value);
            });
            nodeInfo.Attributes.Clear();
            nodeInfo.Attributes.AddRange(attr);
            var key = nodeInfo.PublicKey.ToByteArray().ToHexString();
            if (!NetmapSnapshot.Touch(key, State.EpochCounter()))
            {
                Utility.Log(Name, LogLevel.Info, $"approving network map candidate, key={key}");
                try
                {
                    MorphInvoker.ApprovePeer(addPeerEvent.Node);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Error, $"can't invoke netmap.AddPeer, error={e}");
                }
            }
        }

        public void ProcessUpdateState(UpdatePeerEvent updateStateEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore update peer notification");
                return;
            }
            if (updateStateEvent.Status != (uint)API.Netmap.NodeInfo.Types.State.Offline)
            {
                Utility.Log(Name, LogLevel.Warning, $"node proposes unknown state, ke={updateStateEvent.PublicKey}, status={updateStateEvent.Status}");
                return;
            }
            NetmapSnapshot.Flag(updateStateEvent.PublicKey.ToString());
            try
            {
                MorphInvoker.UpdatePeerState((API.Netmap.NodeInfo.Types.State)updateStateEvent.Status, updateStateEvent.PublicKey.EncodePoint(true));
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't invoke netmap.UpdatePeer, error={e}");
            }
        }
    }
}
