using Akka.Actor;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.Morph.Event;
using Neo.IO;
using Neo.Plugins.util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Neo.FileStorage.Morph.Event.MorphEvent;
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
        private const string UpdateIRNotification = "InnerRingUpdate";

        private const string TxLogPrefix = "mainnet:";
        private const ulong LockAccountLifetime = 20;
        private object lockObj = new object();
        private int mintEmitCacheSize => Settings.Default.MintEmitCacheSize;
        private ulong mintEmitThreshold => Settings.Default.MintEmitThreshold;
        private long mintEmitValue = Settings.Default.MintEmitValue;
        private Dictionary<string, ulong> mintEmitCache;

        public Fixed8ConverterUtil Convert;

        public FsContractProcessor()
        {
            mintEmitCache = new Dictionary<string, ulong>(mintEmitCacheSize);
        }

        public override HandlerInfo[] ListenerHandlers()
        {
            HandlerInfo depositHandler = new HandlerInfo();
            depositHandler.ScriptHashWithType = new ScriptHashWithType() { Type = DepositNotification, ScriptHashValue = FsContractHash };
            depositHandler.Handler = HandleDeposit;

            HandlerInfo withdrwaHandler = new HandlerInfo();
            withdrwaHandler.ScriptHashWithType = new ScriptHashWithType() { Type = WithdrawNotification, ScriptHashValue = FsContractHash };
            withdrwaHandler.Handler = HandleWithdraw;

            HandlerInfo chequeHandler = new HandlerInfo();
            chequeHandler.ScriptHashWithType = new ScriptHashWithType() { Type = ChequeNotification, ScriptHashValue = FsContractHash };
            chequeHandler.Handler = HandleCheque;

            HandlerInfo configHandler = new HandlerInfo();
            configHandler.ScriptHashWithType = new ScriptHashWithType() { Type = ConfigNotification, ScriptHashValue = FsContractHash };
            configHandler.Handler = HandleConfig;

            return new HandlerInfo[] { depositHandler, withdrwaHandler, chequeHandler, configHandler};
        }

        public override ParserInfo[] ListenerParsers()
        {
            //deposit event
            ParserInfo depositParser = new ParserInfo();
            depositParser.ScriptHashWithType = new ScriptHashWithType() { Type = DepositNotification, ScriptHashValue = FsContractHash };
            depositParser.Parser = DepositEvent.ParseDepositEvent;

            //withdraw event
            ParserInfo withdrawParser = new ParserInfo();
            withdrawParser.ScriptHashWithType = new ScriptHashWithType() { Type = WithdrawNotification, ScriptHashValue = FsContractHash };
            withdrawParser.Parser = WithdrawEvent.ParseWithdrawEvent;

            //cheque event
            ParserInfo chequeParser = new ParserInfo();
            chequeParser.ScriptHashWithType = new ScriptHashWithType() { Type = ChequeNotification, ScriptHashValue = FsContractHash };
            chequeParser.Parser = ChequeEvent.ParseChequeEvent;

            //config event
            ParserInfo configParser = new ParserInfo();
            configParser.ScriptHashWithType = new ScriptHashWithType() { Type = ConfigNotification, ScriptHashValue = FsContractHash };
            configParser.Parser = ConfigEvent.ParseConfigEvent;
            return new ParserInfo[] { depositParser, withdrawParser, chequeParser, configParser};
        }

        public void HandleDeposit(IContractEvent morphEvent)
        {
            DepositEvent depositeEvent = (DepositEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, string.Format("notification:type:deposit,value:{0}", depositeEvent.Id.ToHexString()));
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => ProcessDeposit(depositeEvent)) });
        }

        public void HandleWithdraw(IContractEvent morphEvent)
        {
            WithdrawEvent withdrawEvent = (WithdrawEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, string.Format("notification:type:withdraw,value:{0}", withdrawEvent.Id.ToHexString()));
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => ProcessWithdraw(withdrawEvent)) });
        }

        public void HandleCheque(IContractEvent morphEvent)
        {
            ChequeEvent chequeEvent = (ChequeEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, string.Format("notification:type:cheque,value:{0}", chequeEvent.Id.ToHexString()));
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => ProcessCheque(chequeEvent)) });
        }

        public void HandleConfig(IContractEvent morphEvent)
        {
            ConfigEvent configEvent = (ConfigEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, string.Format("notification:type:setConfig,key:{0},value:{1}", configEvent.Key.ToHexString(), configEvent.Value.ToHexString()));
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => ProcessConfig(configEvent)) });
        }

        public void ProcessDeposit(DepositEvent depositeEvent)
        {
            if (!IsActive())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore deposit");
                return;
            }
            try
            {
                List<byte> coment = new List<byte>();
                coment.AddRange(System.Text.Encoding.UTF8.GetBytes(TxLogPrefix));
                coment.AddRange(depositeEvent.Id);
                ContractInvoker.Mint(MorphCli, depositeEvent.To.ToArray(), Convert.ToBalancePrecision(depositeEvent.Amount), coment.ToArray());
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("can't transfer assets to balance contract,{0}", e.Message));
            }

            var curEpoch = EpochState.EpochCounter();
            var receiver = depositeEvent.To;
            lock (lockObj)
            {
                var ok = mintEmitCache.TryGetValue(receiver.ToString(), out ulong value);
                if (ok && ((value + mintEmitThreshold) >= curEpoch))
                    Utility.Log(Name, LogLevel.Warning, string.Format("double mint emission declined,receiver:{0},last_emission:{1},current_epoch:{2}", receiver.ToString(), value.ToString(), curEpoch.ToString()));
                var balance=MorphCli.GasBalance();
                //todo gasBalanceThreshold
                if (balance< 0) Utility.Log(Name, LogLevel.Warning, string.Format("gas balance threshold has been reached,balance:{0},threshold:{1}", balance, 0));
                try
                {
                    MorphCli.TransferGas(depositeEvent.To, mintEmitValue);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Error, string.Format("can't transfer native gas to receiver,{0}", e.Message));
                }
                mintEmitCache.Add(receiver.ToString(), curEpoch);
            }
        }

        public void ProcessWithdraw(WithdrawEvent withdrawEvent)
        {
            if (!IsActive())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore withdraw");
                return;
            }
            if (withdrawEvent.Id.Length < UInt160.Length)
            {
                Utility.Log(Name, LogLevel.Error, "tx id size is less than script hash size");
                return;
            }
            UInt160 lockeAccount = null;
            try
            {
                lockeAccount = new UInt160(withdrawEvent.Id.Take(UInt160.Length).ToArray());
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("can't create lock account,{0}", e.Message));
                return;
            }
            try
            {
                ulong curEpoch = EpochCounter();
                //invoke
                ContractInvoker.LockAsset(MorphCli, withdrawEvent.Id, withdrawEvent.UserAccount, lockeAccount,Convert.ToBalancePrecision(withdrawEvent.Amount), curEpoch + LockAccountLifetime);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("can't lock assets for withdraw,{0}", e.Message));
            }
        }

        public void ProcessCheque(ChequeEvent chequeEvent)
        {
            if (!IsActive())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore cheque");
                return;
            }
            //invoke
            try
            {
                ContractInvoker.Burn(MorphCli, chequeEvent.LockAccount.ToArray(), Convert.ToBalancePrecision(chequeEvent.Amount), System.Text.Encoding.UTF8.GetBytes(TxLogPrefix).Concat(chequeEvent.Id).ToArray());
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("can't transfer assets to fed contract,{0}", e.Message));
            }
        }

        public void ProcessConfig(ConfigEvent configEvent)
        {
            if (!IsActive())
            {
                Utility.Log(Name, LogLevel.Info, "passive mode, ignore deposit");
                return;
            }
            //invoke
            try
            {
                ContractInvoker.SetConfig(MorphCli, configEvent.Id, configEvent.Key, configEvent.Value);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("can't relay set config event,{0}", e.Message));
            }
        }
    }
}
