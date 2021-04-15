using Akka.Actor;
using Neo.Cryptography;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.Morph.Event;
using System;
using System.Threading.Tasks;
using static Neo.FileStorage.Morph.Event.MorphEvent;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class ContainerContractProcessor : BaseProcessor
    {
        public override string Name => "ContainerContractProcessor";
        private const string PutNotification = "containerPut";
        private const string DeleteNotification = "containerDelete";

        public override HandlerInfo[] ListenerHandlers()
        {
            HandlerInfo putHandler = new HandlerInfo();
            putHandler.ScriptHashWithType = new ScriptHashWithType() { Type = PutNotification, ScriptHashValue = ContainerContractHash };
            putHandler.Handler = HandlePut;
            HandlerInfo deleteHandler = new HandlerInfo();
            deleteHandler.ScriptHashWithType = new ScriptHashWithType() { Type = DeleteNotification, ScriptHashValue = ContainerContractHash };
            deleteHandler.Handler = HandleDelete;
            return new HandlerInfo[] { putHandler, deleteHandler };
        }

        public override ParserInfo[] ListenerParsers()
        {
            //container put event
            ParserInfo putParser = new ParserInfo();
            putParser.ScriptHashWithType = new ScriptHashWithType() { Type = PutNotification, ScriptHashValue = ContainerContractHash };
            putParser.Parser = ContainerPutEvent.ParseContainerPutEvent;
            //container delete event
            ParserInfo deleteParser = new ParserInfo();
            deleteParser.ScriptHashWithType = new ScriptHashWithType() { Type = DeleteNotification, ScriptHashValue = ContainerContractHash };
            deleteParser.Parser = ContainerDeleteEvent.ParseContainerDeleteEvent;
            return new ParserInfo[] { putParser, deleteParser };
        }

        public void HandlePut(IContractEvent morphEvent)
        {
            ContainerPutEvent putEvent = (ContainerPutEvent)morphEvent;
            var id = putEvent.RawContainer.Sha256();
            Utility.Log(Name, LogLevel.Info, string.Format("notification:type:container put,id:{0}", Base58.Encode(id)));
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => ProcessContainerPut(putEvent)) });
        }

        public void HandleDelete(IContractEvent morphEvent)
        {
            ContainerDeleteEvent deleteEvent = (ContainerDeleteEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, string.Format("notification:type:container delete,id:{0}", Base58.Encode(deleteEvent.ContainerID)));
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => ProcessContainerDelete(deleteEvent)) });
        }

        public void ProcessContainerPut(ContainerPutEvent putEvent)
        {
            if (!IsActive())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore container put");
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
                Utility.Log(Name, LogLevel.Error, string.Format("could not unmarshal container structure:{0}", e.Message));
            }
            try
            {
                CheckFormat(container);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("container with incorrect format detected:{0}", e.Message));
            }
            try
            {
                ContractInvoker.RegisterContainer(MainCli, putEvent.PublicKey, putEvent.RawContainer, putEvent.Signature);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("can't invoke new container:{0}", e.Message));
            }
        }

        public void ProcessContainerDelete(ContainerDeleteEvent deleteEvent)
        {
            if (!IsActive())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore container put");
                return;
            }
            try
            {
                ContractInvoker.RemoveContainer(MainCli, deleteEvent.ContainerID, deleteEvent.Signature);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("can't invoke delete container:{0}", e.Message));
            }
        }

        public void CheckFormat(Container container)
        {
            if (container.PlacementPolicy is null) throw new Exception("placement policy is nil");
            if (!API.Refs.Version.IsSupportedVersion(container.Version)) throw new Exception("incorrect version");
            if (container.OwnerId.Value.Length != 25) throw new Exception(string.Format("incorrect owner identifier:expected length {0}!={1}", 25, container.OwnerId.Value.Length));
            if (container.Nonce.ToByteArray().Length != 16) throw new Exception("incorrect nonce");
        }
    }
}
