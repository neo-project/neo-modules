using Akka.Actor;
using Neo.FileStorage.InnerRing.Invoke;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoke;
using Neo.Plugins.util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Neo.FileStorage.InnerRing.Invoke.ContractInvoker;
using static Neo.FileStorage.Morph.Event.MorphEvent;
using static Neo.Plugins.util.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class BalanceContractProcessor : IProcessor
    {
        private string name = "BalanceContractProcessor";
        private UInt160 BalanceContractHash => Settings.Default.BalanceContractHash;
        private const string LockNotification = "Lock";

        public IClient Client;
        public IActiveState ActiveState;
        public IActorRef WorkPool;
        public Fixed8ConverterUtil Convert;

        public string Name { get => name; set => name = value; }

        public HandlerInfo[] ListenerHandlers()
        {
            ScriptHashWithType scriptHashWithType = new ScriptHashWithType()
            {
                Type = LockNotification,
                ScriptHashValue = BalanceContractHash
            };
            HandlerInfo handler = new HandlerInfo()
            {
                ScriptHashWithType = scriptHashWithType,
                Handler = HandleLock
            };
            return new HandlerInfo[] { handler };
        }

        public ParserInfo[] ListenerParsers()
        {
            ScriptHashWithType scriptHashWithType = new ScriptHashWithType()
            {
                Type = LockNotification,
                ScriptHashValue = BalanceContractHash
            };
            ParserInfo parser = new ParserInfo()
            {
                ScriptHashWithType = scriptHashWithType,
                Parser = ParseLockEvent,
            };
            return new ParserInfo[] { parser };
        }

        public HandlerInfo[] TimersHandlers()
        {
            return new HandlerInfo[] { };
        }

        public void HandleLock(IContractEvent morphEvent)
        {
            LockEvent lockEvent = (LockEvent)morphEvent;
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("notification", ":");
            pairs.Add("type", "lock");
            pairs.Add("value", lockEvent.Id.ToHexString());
            Neo.Utility.Log(Name, LogLevel.Info, pairs.ParseToString());
            WorkPool.Tell(new NewTask() { process = name, task = new Task(() => ProcessLock(lockEvent)) });
        }

        public void ProcessLock(LockEvent lockEvent)
        {
            if (!IsActive())
            {
                Neo.Utility.Log(Name, LogLevel.Info, "passive mode, ignore balance lock");
                return;
            }
            //invoke
            try
            {
                ContractInvoker.CashOutCheque(Client, lockEvent.Id, Convert.ToFixed8(lockEvent.Amount), lockEvent.UserAccount, lockEvent.LockAccount);
            }
            catch (Exception e)
            {
                Neo.Utility.Log(Name, LogLevel.Error, string.Format("can't send lock asset tx:{0}" + e.Message));
            }
        }

        public bool IsActive()
        {
            return ActiveState.IsActive();
        }
    }
}
