using Akka.Actor;
using Neo.Plugins.FSStorage.innerring.invoke;
using Neo.Plugins.FSStorage.morph.invoke;
using Neo.Plugins.Innerring.Processors;
using Neo.Plugins.util;
using System;
using System.Threading.Tasks;
using static Neo.Plugins.FSStorage.MorphEvent;
using static Neo.Plugins.util.WorkerPool;

namespace Neo.Plugins.FSStorage.innerring.processors
{
    public class BalanceContractProcessor : BaseProcessor
    {
        public override string Name => "BalanceContractProcessor";
        private const string LockNotification = "Lock";
        public Fixed8ConverterUtil Convert;

        public override HandlerInfo[] ListenerHandlers()
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

        public override ParserInfo[] ListenerParsers()
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

        public void HandleLock(IContractEvent morphEvent)
        {
            LockEvent lockEvent = (LockEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, string.Format("notification:type:lock,value:{0}", lockEvent.Id.ToHexString()));
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => ProcessLock(lockEvent)) });
        }

        public void ProcessLock(LockEvent lockEvent)
        {
            if (!IsActive())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore balance lock");
                return;
            }
            try
            {
                ContractInvoker.CashOutCheque(MainCli, lockEvent.Id, Convert.ToFixed8(lockEvent.Amount), lockEvent.UserAccount, lockEvent.LockAccount);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("can't send lock asset tx:{0}", e.Message));
            }
        }
    }
}
