using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Reputation;
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
            ScriptHashWithType scriptHashWithType = new()
            {
                Type = PutReputationNotification,
                ScriptHashValue = ReputationContractHash
            };
            HandlerInfo handler = new()
            {
                ScriptHashWithType = scriptHashWithType,
                Handler = HandlePutReputation
            };
            return new HandlerInfo[] { handler };
        }

        public override ParserInfo[] ListenerParsers()
        {
            ScriptHashWithType scriptHashWithType = new()
            {
                Type = PutReputationNotification,
                ScriptHashValue = ReputationContractHash
            };
            ParserInfo parser = new()
            {
                ScriptHashWithType = scriptHashWithType,
                Parser = ReputationPutEvent.ParseReputationPutEvent,
            };
            return new ParserInfo[] { parser };
        }

        public void HandlePutReputation(ContractEvent morphEvent)
        {
            ReputationPutEvent reputationPutEvent = (ReputationPutEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, $"event, type=reputation_put, peer_id={reputationPutEvent.PeerID.ToHexString()}");
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
                Utility.Log(Name, LogLevel.Info, $"ignore reputation value, trust_epoch={reputationPutEvent.Epoch}, local_epoch={currentEpoch}");
                return;
            }
            if (!reputationPutEvent.Trust.Signature.VerifyMessagePart(reputationPutEvent.Trust.Body))
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
                Utility.Log(Name, LogLevel.Info, $"ignore reputation value, reason:wrong manager, error:{e}");
                return;
            }
            try
            {
                MorphInvoker.PutReputation(reputationPutEvent.Epoch, reputationPutEvent.PeerID, reputationPutEvent.Trust.ToByteArray());
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Info, $"can't send approval tx for reputation value, peer_id={reputationPutEvent.PeerID.ToHexString()}, error={e}");
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
            throw new InvalidOperationException("got manager that is incorrect for peer");
        }
    }
}
