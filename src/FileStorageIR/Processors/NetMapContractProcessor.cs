using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Google.Protobuf.Collections;
using Neo.Cryptography.ECC;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.InnerRing.Utils;
using Neo.FileStorage.InnerRing.Utils.Locode.Column;
using Neo.FileStorage.InnerRing.Utils.Locode.Db;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Listen;
using static Neo.FileStorage.InnerRing.Events.MorphEvent;
using static Neo.FileStorage.InnerRing.Timer.TimerTickEvent;
using static Neo.FileStorage.Morph.Event.MorphEvent;
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
        public Validator NodeValidator;
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
            HandleNewAudit(new StartEvent() { epoch = newEpochEvent.EpochNumber });
            HandleAuditSettlements(new AuditStartEvent() { epoch = newEpochEvent.EpochNumber });
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
                Utility.Log(Name, LogLevel.Warning, $"node proposes unknown state, ke={updateStateEvent.PublicKey.EncodePoint(true).ToHexString()}, status={updateStateEvent.Status}");
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
    public class CleanupTable
    {
        private readonly object lockObject = new();
        private Dictionary<string, EpochStamp> lastAccess = new();
        private bool enabled;
        private readonly ulong threshold;

        public bool Enabled { get => enabled; set => enabled = value; }

        public CleanupTable(bool enabled, ulong threshold)
        {
            this.enabled = enabled;
            this.threshold = threshold;
        }

        public void Update(API.Netmap.NodeInfo[] snapshot, ulong now)
        {
            lock (lockObject)
            {
                var newMap = new Dictionary<string, EpochStamp>();
                foreach (var item in snapshot)
                {
                    var key = item.PublicKey.ToByteArray().ToHexString();
                    if (lastAccess.TryGetValue(key, out EpochStamp access))
                    {
                        access.RemoveFlag = false;
                        newMap[key] = access;
                    }
                    else
                    {
                        newMap[key] = new EpochStamp() { Epoch = now };
                    }
                }
                lastAccess = newMap;
            }
        }

        public bool Touch(string key, ulong now)
        {
            lock (lockObject)
            {
                EpochStamp epochStamp = null;
                bool result = false;
                if (lastAccess.TryGetValue(key, out EpochStamp access))
                {
                    epochStamp = access;
                    result = !epochStamp.RemoveFlag;
                }
                else
                {
                    epochStamp = new EpochStamp();
                }
                epochStamp.RemoveFlag = false;
                if (now > epochStamp.Epoch)
                {
                    epochStamp.Epoch = now;
                }
                lastAccess[key] = epochStamp;
                return result;
            }
        }

        public void Flag(string key)
        {
            lock (lockObject)
            {
                if (lastAccess.TryGetValue(key, out EpochStamp access))
                {
                    access.RemoveFlag = true;
                    lastAccess[key] = access;
                }
                else
                {
                    lastAccess[key] = new EpochStamp() { RemoveFlag = true };
                }
            }
        }

        public void ForEachRemoveCandidate(ulong epoch, Action<string> f)
        {
            lock (lockObject)
            {
                foreach (var item in lastAccess)
                {
                    var key = item.Key;
                    var access = item.Value;
                    if (epoch - access.Epoch > threshold)
                    {
                        access.RemoveFlag = true;
                        lastAccess[key] = access;
                        f(key);
                    }
                }
            }
        }
    }

    public class EpochStamp
    {
        private ulong epoch;
        private bool removeFlag;

        public ulong Epoch { get => epoch; set => epoch = value; }
        public bool RemoveFlag { get => removeFlag; set => removeFlag = value; }
    }

    public class Validator
    {
        private Dictionary<string, AttrDescriptor> mAttr;
        private StorageDB dB;

        public Validator(StorageDB dB)
        {
            this.dB = dB;
            this.mAttr = new Dictionary<string, AttrDescriptor>()
                {
                    { Node.AttributeCountryCode,new AttrDescriptor(){ converter=AttrDescriptor.CountryCodeValue} },
                    { Node.AttributeCountry,new AttrDescriptor(){ converter=AttrDescriptor.CountryValue} },
                    { Node.AttributeLocation,new AttrDescriptor(){ converter=AttrDescriptor.LocationValue} },
                    { Node.AttributeSubDivCode,new AttrDescriptor(){ converter=AttrDescriptor.SubDivCodeValue,optional=true} },
                    { Node.AttributeSubDiv,new AttrDescriptor(){ converter=AttrDescriptor.SubDivValue,optional=true} },
                    { Node.AttributeContinent,new AttrDescriptor(){ converter=AttrDescriptor.ContinentValue} },
                };
        }

        public void VerifyAndUpdate(API.Netmap.NodeInfo n)
        {
            var tAttr = UniqueAttributes(n.Attributes.GetEnumerator());
            if (!tAttr.TryGetValue(Node.AttributeUNLOCODE, out var attrLocode)) return;
            var lc = LOCODE.FromString(attrLocode.Value);
            (Key, Record) record = dB.Get(lc);
            foreach (var attr in mAttr)
            {
                var attrVal = attr.Value.converter(record);
                if (attrVal == "")
                {
                    if (!attr.Value.optional)
                        throw new Exception("missing required attribute in DB record");
                    continue;
                }
                API.Netmap.NodeInfo.Types.Attribute a = new();
                a.Key = attr.Key;
                a.Value = attrVal;
                tAttr[attr.Key] = a;
            }
            var ass = new List<API.Netmap.NodeInfo.Types.Attribute>();
            foreach (var item in tAttr)
                ass.Add(item.Value);
            n.Attributes.Clear();
            n.Attributes.AddRange(ass);
        }

        public Dictionary<string, API.Netmap.NodeInfo.Types.Attribute> UniqueAttributes(IEnumerator<API.Netmap.NodeInfo.Types.Attribute> attributes)
        {
            Dictionary<string, API.Netmap.NodeInfo.Types.Attribute> tAttr = new();
            while (attributes.MoveNext())
            {
                var attr = attributes.Current;
                tAttr[attr.Key] = attr;
            }
            return tAttr;
        }
    }
    public class AttrDescriptor
    {
        public Func<(Key, Record), string> converter;
        public bool optional;
        public static string CountryCodeValue((Key, Record) record)
        {
            return string.Concat<char>(record.Item1.CountryCode.Symbols());
        }

        public static string CountryValue((Key, Record) record)
        {
            return record.Item2.CountryName;
        }

        public static string LocationCodeValue((Key, Record) record)
        {
            return string.Concat<char>(record.Item1.LocationCode.Symbols());
        }

        public static string LocationValue((Key, Record) record)
        {
            return record.Item2.LocationName;
        }

        public static string SubDivCodeValue((Key, Record) record)
        {
            return record.Item2.SubDivCode;
        }

        public static string SubDivValue((Key, Record) record)
        {
            return record.Item2.SubDivName;
        }

        public static string ContinentValue((Key, Record) record)
        {
            return record.Item2.Continent.String();
        }
    }
}
