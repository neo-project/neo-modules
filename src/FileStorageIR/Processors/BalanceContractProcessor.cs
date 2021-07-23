using System;
using System.Threading.Tasks;
using Akka.Actor;
using Neo.FileStorage.InnerRing.Utils;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event;
using Neo.FileStorage.Listen.Event.Morph;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class BalanceContractProcessor : BaseProcessor
    {
        public override string Name => "BalanceContractProcessor";
        private const string LockNotification = "Lock";
        public Fixed8ConverterUtil Convert;

        public override HandlerInfo[] ListenerHandlers()
        {
            ScriptHashWithType scriptHashWithType = new()
            {
                Type = LockNotification,
                ScriptHashValue = BalanceContractHash
            };
            HandlerInfo handler = new()
            {
                ScriptHashWithType = scriptHashWithType,
                Handler = HandleLock
            };
            return new HandlerInfo[] { handler };
        }

        public override ParserInfo[] ListenerParsers()
        {
            ScriptHashWithType scriptHashWithType = new()
            {
                Type = LockNotification,
                ScriptHashValue = BalanceContractHash
            };
            ParserInfo parser = new()
            {
                ScriptHashWithType = scriptHashWithType,
                Parser = LockEvent.ParseLockEvent,
            };
            return new ParserInfo[] { parser };
        }

        public void HandleLock(ContractEvent morphEvent)
        {
            LockEvent lockEvent = (LockEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, $"event, type=lock, value={lockEvent.Id}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessLock(lockEvent)) });
        }

        public void ProcessLock(LockEvent lockEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore balance lock");
                return;
            }
            try
            {
                MainInvoker.CashOutCheque(lockEvent.Id, Convert.ToFixed8(lockEvent.Amount), lockEvent.UserAccount, lockEvent.LockAccount);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't send lock asset, error={e}");
            }
        }
    }
}
