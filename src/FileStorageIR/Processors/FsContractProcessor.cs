using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Neo.FileStorage.Cache;
using Neo.FileStorage.InnerRing.Utils;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.IO;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class FsContractProcessor : BaseProcessor
    {
        public override string Name => "FsContractProcessor";

        private const string DepositNotification = "Deposit";
        private const string WithdrawNotification = "Withdraw";
        private const string ChequeNotification = "Cheque";
        private const string ConfigNotification = "SetConfig";
        private const string BindNotification = "Bind";
        private const string UnBindNotification = "Unbind";

        private const string TxLogPrefix = "mainnet:";
        private const ulong LockAccountLifetime = 20;
        private readonly object lockObj = new object();
        private int MintEmitCacheSize => Settings.Default.MintEmitCacheSize;
        private ulong MintEmitThreshold => Settings.Default.MintEmitThreshold;
        private long GasBalanceThreshold => Settings.Default.GasBalanceThreshold;
        private long MintEmitValue = Settings.Default.MintEmitValue;
        private readonly LRUCache<string, ulong> mintEmitCache;

        public Fixed8ConverterUtil Convert;

        public FsContractProcessor()
        {
            mintEmitCache = new LRUCache<string, ulong>(MintEmitCacheSize);
        }

        public override HandlerInfo[] ListenerHandlers()
        {
            HandlerInfo depositHandler = new();
            depositHandler.ScriptHashWithType = new ScriptHashWithType() { Type = DepositNotification, ScriptHashValue = FsContractHash };
            depositHandler.Handler = HandleDeposit;

            HandlerInfo withdrwaHandler = new();
            withdrwaHandler.ScriptHashWithType = new ScriptHashWithType() { Type = WithdrawNotification, ScriptHashValue = FsContractHash };
            withdrwaHandler.Handler = HandleWithdraw;

            HandlerInfo chequeHandler = new();
            chequeHandler.ScriptHashWithType = new ScriptHashWithType() { Type = ChequeNotification, ScriptHashValue = FsContractHash };
            chequeHandler.Handler = HandleCheque;

            HandlerInfo configHandler = new();
            configHandler.ScriptHashWithType = new ScriptHashWithType() { Type = ConfigNotification, ScriptHashValue = FsContractHash };
            configHandler.Handler = HandleConfig;

            HandlerInfo bindHandler = new();
            bindHandler.ScriptHashWithType = new ScriptHashWithType() { Type = BindNotification, ScriptHashValue = FsContractHash };
            bindHandler.Handler = HandleBind;

            HandlerInfo unbindHandler = new();
            unbindHandler.ScriptHashWithType = new ScriptHashWithType() { Type = UnBindNotification, ScriptHashValue = FsContractHash };
            unbindHandler.Handler = HandleUnBind;

            return new HandlerInfo[] { depositHandler, withdrwaHandler, chequeHandler, configHandler, bindHandler, unbindHandler };
        }

        public override ParserInfo[] ListenerParsers()
        {
            ParserInfo depositParser = new();
            depositParser.ScriptHashWithType = new ScriptHashWithType() { Type = DepositNotification, ScriptHashValue = FsContractHash };
            depositParser.Parser = DepositEvent.ParseDepositEvent;

            ParserInfo withdrawParser = new();
            withdrawParser.ScriptHashWithType = new ScriptHashWithType() { Type = WithdrawNotification, ScriptHashValue = FsContractHash };
            withdrawParser.Parser = WithdrawEvent.ParseWithdrawEvent;

            ParserInfo chequeParser = new();
            chequeParser.ScriptHashWithType = new ScriptHashWithType() { Type = ChequeNotification, ScriptHashValue = FsContractHash };
            chequeParser.Parser = ChequeEvent.ParseChequeEvent;

            ParserInfo configParser = new();
            configParser.ScriptHashWithType = new ScriptHashWithType() { Type = ConfigNotification, ScriptHashValue = FsContractHash };
            configParser.Parser = ConfigEvent.ParseConfigEvent;

            ParserInfo bindParser = new();
            bindParser.ScriptHashWithType = new ScriptHashWithType() { Type = BindNotification, ScriptHashValue = FsContractHash };
            bindParser.Parser = BindEvent.ParseBindEvent;

            ParserInfo unbindParser = new();
            unbindParser.ScriptHashWithType = new ScriptHashWithType() { Type = UnBindNotification, ScriptHashValue = FsContractHash };
            unbindParser.Parser = BindEvent.ParseBindEvent;
            return new ParserInfo[] { depositParser, withdrawParser, chequeParser, configParser, bindParser, unbindParser };
        }

        public void HandleDeposit(ContractEvent morphEvent)
        {
            DepositEvent depositeEvent = (DepositEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, $"event, type=deposit, id={depositeEvent.Id}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessDeposit(depositeEvent)) });
        }

        public void HandleWithdraw(ContractEvent morphEvent)
        {
            WithdrawEvent withdrawEvent = (WithdrawEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, $"event, type=withdraw, id={withdrawEvent.Id}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessWithdraw(withdrawEvent)) });
        }

        public void HandleCheque(ContractEvent morphEvent)
        {
            ChequeEvent chequeEvent = (ChequeEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, $"event, type=cheque, value={chequeEvent.Id}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessCheque(chequeEvent)) });
        }

        public void HandleConfig(ContractEvent morphEvent)
        {
            ConfigEvent configEvent = (ConfigEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, $"event, type=setConfig, key={configEvent.Key}, value={configEvent.Value}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessConfig(configEvent)) });
        }

        public void HandleBind(ContractEvent morphEvent)
        {
            BindEvent bindEvent = (BindEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, "event, type=bind");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessBind(bindEvent, true)) });
        }

        public void HandleUnBind(ContractEvent morphEvent)
        {
            BindEvent bindEvent = (BindEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, "event, type=unbind");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessBind(bindEvent, false)) });
        }

        public void ProcessDeposit(DepositEvent depositeEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore deposit");
                return;
            }
            try
            {
                MorphInvoker.Mint(depositeEvent.To.ToArray(), Convert.ToBalanceDecimals(depositeEvent.Amount), Utility.StrictUTF8.GetBytes(TxLogPrefix).Concat(depositeEvent.Id).ToArray());
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't transfer assets to balance contract, error={e}");
            }

            var curEpoch = State.EpochCounter();
            var receiver = depositeEvent.To;
            lock (lockObj)
            {
                if (mintEmitCache.TryGet(receiver.ToString(), out ulong value) && ((value + MintEmitThreshold) >= curEpoch))
                {
                    Utility.Log(Name, LogLevel.Warning, $"double mint emission declined, receiver={receiver}, last_emission={value}, current_epoch={curEpoch}");
                    return;
                }
                long balance;
                try
                {
                    balance = MorphInvoker.GasBalance();
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, $"can't get gas balance of the node, error={e}");
                    return;
                }
                if (balance < GasBalanceThreshold)
                {
                    Utility.Log(Name, LogLevel.Warning, $"gas balance threshold has been reached, balance={balance}, threshold={GasBalanceThreshold}");
                    return;
                }
                try
                {
                    MorphInvoker.TransferGas(depositeEvent.To, MintEmitValue);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Error, $"can't transfer native gas to receiver, error={e}");
                }
                mintEmitCache.Add(receiver.ToString(), curEpoch);
            }
        }

        public void ProcessWithdraw(WithdrawEvent withdrawEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore withdraw");
                return;
            }
            if (withdrawEvent.Id.Length < UInt160.Length)
            {
                Utility.Log(Name, LogLevel.Error, "tx id size is less than script hash size");
                return;
            }
            UInt160 lockeAccount;
            try
            {
                lockeAccount = new UInt160(withdrawEvent.Id.Take(UInt160.Length).ToArray());
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't create lock account, error={e}");
                return;
            }
            try
            {
                ulong curEpoch = State.EpochCounter();
                MorphInvoker.LockAsset(withdrawEvent.Id, withdrawEvent.UserAccount, lockeAccount, Convert.ToBalanceDecimals(withdrawEvent.Amount), curEpoch + LockAccountLifetime);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't lock assets for withdraw, error={e}");
            }
        }

        public void ProcessCheque(ChequeEvent chequeEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore cheque");
                return;
            }
            try
            {
                MorphInvoker.Burn(chequeEvent.LockAccount.ToArray(), Convert.ToBalanceDecimals(chequeEvent.Amount), System.Text.Encoding.UTF8.GetBytes(TxLogPrefix).Concat(chequeEvent.Id).ToArray());
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't transfer assets to fed contract, error={e}");
            }
        }

        public void ProcessConfig(ConfigEvent configEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "passive mode, ignore deposit");
                return;
            }
            try
            {
                MorphInvoker.SetConfig(configEvent.Id, configEvent.Key, configEvent.Value);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't relay set config event, error={e}");
            }
        }

        public void ProcessBind(BindEvent bindEvent, bool bind)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "passive mode, ignore bind");
                return;
            }
            try
            {
                if (bind)
                    MorphInvoker.AddKeys(bindEvent.UserAccount, bindEvent.Keys);
                else
                    MorphInvoker.RemoveKeys(bindEvent.UserAccount, bindEvent.Keys);
            }
            catch
            {
                Utility.Log(Name, LogLevel.Error, "can't approve bind/unbind event");
            }
        }
    }
}
