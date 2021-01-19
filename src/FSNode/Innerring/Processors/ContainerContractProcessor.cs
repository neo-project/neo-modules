using Akka.Actor;
using Neo.Cryptography;
using Neo.Plugins.FSStorage.innerring.invoke;
using Neo.Plugins.FSStorage.morph.invoke;
using Neo.Plugins.util;
using NeoFS.API.v2.Container;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Neo.Plugins.FSStorage.innerring.invoke.ContractInvoker;
using static Neo.Plugins.FSStorage.MorphEvent;
using static Neo.Plugins.util.WorkerPool;

namespace Neo.Plugins.FSStorage.innerring.processors
{
    public class ContainerContractProcessor : IProcessor
    {
        private string name = "ContainerContractProcessor";
        private static UInt160 ContainerContractHash = Settings.Default.ContainerContractHash;
        private const string PutNotification = "containerPut";
        private const string DeleteNotification = "containerDelete";

        public IClient Client;
        public IActiveState ActiveState;
        public IActorRef WorkPool;
        public string Name { get => name; set => name = value; }

        public HandlerInfo[] ListenerHandlers()
        {
            HandlerInfo putHandler = new HandlerInfo();
            putHandler.ScriptHashWithType = new ScriptHashWithType() { Type = PutNotification, ScriptHashValue = ContainerContractHash };
            putHandler.Handler = HandlePut;
            HandlerInfo deleteHandler = new HandlerInfo();
            deleteHandler.ScriptHashWithType = new ScriptHashWithType() { Type = DeleteNotification, ScriptHashValue = ContainerContractHash };
            deleteHandler.Handler = HandleDelete;
            return new HandlerInfo[] { putHandler, deleteHandler };
        }

        public ParserInfo[] ListenerParsers()
        {
            //container put event
            ParserInfo putParser = new ParserInfo();
            putParser.ScriptHashWithType = new ScriptHashWithType() { Type = PutNotification, ScriptHashValue = ContainerContractHash };
            putParser.Parser = MorphEvent.ParseContainerPutEvent;
            //container delete event
            ParserInfo deleteParser = new ParserInfo();
            deleteParser.ScriptHashWithType = new ScriptHashWithType() { Type = DeleteNotification, ScriptHashValue = ContainerContractHash };
            deleteParser.Parser = MorphEvent.ParseContainerDeleteEvent;
            return new ParserInfo[] { putParser, deleteParser };
        }

        public HandlerInfo[] TimersHandlers()
        {
            return new HandlerInfo[] { };
        }

        public void HandlePut(IContractEvent morphEvent)
        {
            ContainerPutEvent putEvent = (ContainerPutEvent)morphEvent;
            var id = putEvent.RawContainer.Sha256();
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("notification", ":");
            pairs.Add("type", "container put");
            pairs.Add("id", Base58.Encode(id));
            Neo.Utility.Log(Name, LogLevel.Info, pairs.ParseToString());
            //send event to workpool
            WorkPool.Tell(new NewTask() { process = name, task = new Task(() => ProcessContainerPut(putEvent)) });
        }

        public void HandleDelete(IContractEvent morphEvent)
        {
            ContainerDeleteEvent deleteEvent = (ContainerDeleteEvent)morphEvent;
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("notification", ":");
            pairs.Add("type", "container delete");
            pairs.Add("id", Base58.Encode(deleteEvent.ContainerID));
            Neo.Utility.Log(Name, LogLevel.Info, pairs.ParseToString());
            //send event to workpool
            WorkPool.Tell(new NewTask() { process = name, task = new Task(() => ProcessContainerDelete(deleteEvent)) });
        }

        public void ProcessContainerPut(ContainerPutEvent putEvent)
        {
            if (!IsActive())
            {
                Neo.Utility.Log(Name, LogLevel.Info, "passive mode, ignore container put");
                return;
            }
            var cnrData = putEvent.RawContainer;
            Container container = null;
            try
            {
                container = Container.Parser.ParseFrom(cnrData);
            }
            catch (Exception e)
            {
                Neo.Utility.Log(Name, LogLevel.Error, string.Format("could not unmarshal container structure:{0}", e.Message));
            }
            try
            {
                CheckFormat(container);
            }
            catch (Exception e)
            {
                Neo.Utility.Log(Name, LogLevel.Error, string.Format("container with incorrect format detected:{0}", e.Message));
            }
            //invoke
            try
            {
                ContractInvoker.RegisterContainer(Client, new ContainerParams()
                {
                    Key = putEvent.PublicKey,
                    Container = putEvent.RawContainer,
                    Signature = putEvent.Signature
                });
            }
            catch (Exception e)
            {
                Neo.Utility.Log(Name, LogLevel.Error, string.Format("can't invoke new container:{0}", e.Message));
            }
        }

        public void ProcessContainerDelete(ContainerDeleteEvent deleteEvent)
        {
            if (!IsActive())
            {
                Neo.Utility.Log(Name, LogLevel.Info, "passive mode, ignore container put");
                return;
            }
            //invoke
            try
            {
                ContractInvoker.RemoveContainer(Client, new RemoveContainerParams()
                {
                    ContainerID = deleteEvent.ContainerID,
                    Signature = deleteEvent.Signature
                });
            }
            catch (Exception e)
            {
                Neo.Utility.Log(Name, LogLevel.Error, string.Format("can't invoke delete container:{0}", e.Message));
            }
        }

        public bool IsActive()
        {
            return ActiveState.IsActive();
        }

        public void CheckFormat(NeoFS.API.v2.Container.Container container)
        {
            if (container.PlacementPolicy is null) throw new Exception("placement policy is nil");
            if (container.OwnerId.Value.Length != 25) throw new Exception("incorrect owner identifier");
            if (container.Nonce.ToByteArray().Length != 16) throw new Exception("incorrect nonce");
        }
    }
}
