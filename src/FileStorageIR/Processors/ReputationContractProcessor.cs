using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Google.Protobuf;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Listen;
using Neo.FileStorage.Reputation;
using static Neo.FileStorage.Morph.Event.MorphEvent;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class ReputationContractProcessor : BaseProcessor
    {
        public override string Name => "ReputationContractProcessor";
        private const string PutReputationNotification = "reputationPut";
        public ManagerBuilder mngBuilder;

        public override HandlerInfo[] ListenerHandlers()
        {
            ScriptHashWithType scriptHashWithType = new ScriptHashWithType()
            {
                Type = PutReputationNotification,
                ScriptHashValue = ReputationContractHash
            };
            HandlerInfo handler = new HandlerInfo()
            {
                ScriptHashWithType = scriptHashWithType,
                Handler = HandlePutReputation
            };
            return new HandlerInfo[] { handler };
        }

        public override ParserInfo[] ListenerParsers()
        {
            ScriptHashWithType scriptHashWithType = new ScriptHashWithType()
            {
                Type = PutReputationNotification,
                ScriptHashValue = ReputationContractHash
            };
            ParserInfo parser = new ParserInfo()
            {
                ScriptHashWithType = scriptHashWithType,
                Parser = ReputationPutEvent.ParseReputationPutEvent,
            };
            return new ParserInfo[] { parser };
        }

        public void HandlePutReputation(IContractEvent morphEvent)
        {
            ReputationPutEvent reputationPutEvent = (ReputationPutEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, string.Format("notification:type:reputation put,peer_id:{0}", reputationPutEvent.PeerID.ToHexString()));
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessPut(reputationPutEvent)) });
        }

        public void ProcessPut(ReputationPutEvent reputationPutEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore reputation put notification");
                return;
            }
            var currentEpoch = State.EpochCounter();
            if (reputationPutEvent.Epoch >= currentEpoch)
            {
                Utility.Log(Name, LogLevel.Info, string.Format("ignore reputation value, trust_epoch:{0},local_epoch:{1}", reputationPutEvent.Epoch, currentEpoch));
                return;
            }
            if (!API.Cryptography.SignExtension.VerifyMessagePart(reputationPutEvent.Trust.Signature, reputationPutEvent.Trust.Body))
            {
                Utility.Log(Name, LogLevel.Info, "ignore reputation value, reason:invalid signature");
                return;
            }
            try
            {
                CheckManagers(reputationPutEvent.Epoch, reputationPutEvent.Trust.Body.Manager, reputationPutEvent.PeerID);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Info, $"ignore reputation value, reason:wrong manager,error:{e}");
                return;
            }
            try
            {
                MorphCli.PutReputation(reputationPutEvent.Epoch, reputationPutEvent.PeerID, reputationPutEvent.Trust.ToByteArray());
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Info, string.Format("can't send approval tx for reputation value,peer_id:{0},error:{1}", reputationPutEvent.PeerID.ToHexString(), e.Message));
            }
        }

        public void CheckManagers(ulong epoch, PeerID mng, PeerID peer)
        {
            List<API.Netmap.NodeInfo> mm = mngBuilder.BuilderManagers(epoch, peer);
            foreach (var m in mm)
            {
                if (mng.PublicKey.ToByteArray().SequenceEqual(m.ToByteArray()))
                    return;
            }
            throw new Exception("got manager that is incorrect for peer");
        }
    }
}
