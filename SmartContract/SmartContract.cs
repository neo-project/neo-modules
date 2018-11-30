using Neo.Wallets;
using System;
using Neo.Ledger;
using Neo.VM;
using System.Linq;
using Neo.SmartContract;
using Neo.Network.P2P.Payloads;
using Neo.IO.Json;
using Neo.Network.P2P;
using Neo.Compiler;
using Akka.Actor;
using System.IO;
using System.Collections.Generic;

namespace Neo.Plugins
{
    public class SmartContract : Plugin, ISmartContractPlugin
    {
        private Wallet wallet = null;
        private NeoSystem system = null;

        private static readonly Fixed8 net_fee = Fixed8.FromDecimal(0.001m);

        protected override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length == 0) return false;
            try
            {
                switch (args[0].ToLower())
                {
                    case "help":
                        return OnHelp(args);
                    case "compile":
                        return OnCompile(args);
                    case "deploy":
                        return OnDeploy(args);
                    case "invoke":
                        return OnInvoke(args);
                    case "test":
                        return OnTest(args);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.Message}\n{e.StackTrace}");
            }
            return false;
        }

        private bool OnTest(string[] args)
        {
            if (args.Length < 2) return false;
            switch (args[1].ToLower())
            {
                case "deploy":
                    return OnTestDeploy(args);
                case "invoke":
                    return OnTestInvoke(args);
            }
            return false;
        }

        public void Init(Wallet wallet, NeoSystem system)
        {
            this.wallet = wallet;
            this.system = system;
        }

        private bool OnCompile(string[] parameters)
        {
            if (parameters.Length < 2)
            {
                Console.WriteLine("error");
                return true;
            }

            string[] args = new string[1];
            Console.Write("[Whether NEP-8(y/N)]> ");
            bool isNep8 = Console.ReadLine() == "y" ? true : false;

            if (!isNep8)
            {
                args = new string[2];
                args[1] = "--compatible";
            }
            args[0] = parameters[1];
            Program.Main(args);

            return true;
        }
        private bool OnDeploy(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("error");
                return true;
            }
            if (wallet == null)
            {
                Console.WriteLine("No Wallet Open");
                return true;
            }

            byte[] script;
            try
            {
                string path = args[1];
                script = File.ReadAllBytes(path);
            }
            catch (Exception)
            {
                Console.WriteLine("File Error");
                return true;
            }

            byte[] parameter_list = new byte[0];
            ContractParameterType return_type = new ContractParameterType();
            ContractPropertyState properties = ContractPropertyState.NoProperty;

            string[] keys = { "Parameter List", "Return Type", "Name", "Version", "Author", "Email", "Properties(Storage, Dyncall, Payable)", "Description" };
            Dictionary<string, string> values = new Dictionary<string, string>();

            foreach (string key in keys)
            {
                Console.Write($"[{key}]> ");
                values.Add(key, Console.ReadLine());
            }

            try
            {
                parameter_list = values["Parameter List"].HexToBytes();
                return_type = values["Return Type"].HexToBytes().Select(p => (ContractParameterType?)p).FirstOrDefault() ?? ContractParameterType.Void;

                if (values["Properties(Storage, Dyncall, Payable)"][0] == 'T') properties |= ContractPropertyState.HasStorage;
                if (values["Properties(Storage, Dyncall, Payable)"][1] == 'T') properties |= ContractPropertyState.HasDynamicInvoke;
                if (values["Properties(Storage, Dyncall, Payable)"][2] == 'T') properties |= ContractPropertyState.Payable;
            }
            catch (Exception)
            {
                Console.WriteLine("Parameters Error");
                return true;
            }

            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Neo.Contract.Create", script, parameter_list, return_type, properties, values["Name"], values["Version"], values["Author"], values["Email"], values["Description"]);
                script = sb.ToArray();
            }
            InvocationTransaction tx = new InvocationTransaction();
            tx.Version = 1;
            tx.Script = script;
            if (tx.Attributes == null) tx.Attributes = new TransactionAttribute[0];
            if (tx.Inputs == null) tx.Inputs = new CoinReference[0];
            if (tx.Outputs == null) tx.Outputs = new TransactionOutput[0];
            if (tx.Witnesses == null) tx.Witnesses = new Witness[0];

            ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, testMode: true);

            LogEngine(engine);

            if (engine.State.HasFlag(VMState.FAULT))
            {
                Console.WriteLine("Execution Failed");
                return true;
            }

            tx.Gas = engine.GasConsumed - Fixed8.FromDecimal(10);
            if (tx.Gas < Fixed8.Zero) tx.Gas = Fixed8.Zero;
            tx.Gas = tx.Gas.Ceiling();
            Fixed8 fee = tx.Gas.Equals(Fixed8.Zero) ? net_fee : tx.Gas;

            tx = wallet.MakeTransaction(tx, fee: fee);
            ContractParametersContext context = new ContractParametersContext(tx);
            wallet.Sign(context);
            wallet.ApplyTransaction(tx);

            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                wallet.ApplyTransaction(tx);
                system.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                Console.WriteLine($"Relayed Transaction: {tx.ToJson()}");
            }
            else
            {
                Console.WriteLine(context.ToJson());
            }
            return true;
        }
        private bool OnTestDeploy(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("error");
                return true;
            }

            byte[] script;
            try
            {
                string path = args[2];
                script = File.ReadAllBytes(path);
            }
            catch (Exception)
            {
                Console.WriteLine("File Error");
                return true;
            }

            byte[] parameter_list = new byte[0];
            ContractParameterType return_type = new ContractParameterType();
            ContractPropertyState properties = ContractPropertyState.NoProperty;

            string[] keys = { "Parameter List", "Return Type", "Name", "Version", "Author", "Email", "Properties(Storage, Dyncall, Payable)", "Description" };
            Dictionary<string, string> values = new Dictionary<string, string>();

            foreach (string key in keys)
            {
                Console.Write($"[{key}]> ");
                values.Add(key, Console.ReadLine());
            }

            try
            {
                parameter_list = values["Parameter List"].HexToBytes();
                return_type = values["Return Type"].HexToBytes().Select(p => (ContractParameterType?)p).FirstOrDefault() ?? ContractParameterType.Void;

                if (values["Properties(Storage, Dyncall, Payable)"][0] == 'T') properties |= ContractPropertyState.HasStorage;
                if (values["Properties(Storage, Dyncall, Payable)"][1] == 'T') properties |= ContractPropertyState.HasDynamicInvoke;
                if (values["Properties(Storage, Dyncall, Payable)"][2] == 'T') properties |= ContractPropertyState.Payable;
            }
            catch (Exception)
            {
                Console.WriteLine("Parameters Error");
                return true;
            }

            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Neo.Contract.Create", script, parameter_list, return_type, properties, values["Name"], values["Version"], values["Author"], values["Email"], values["Description"]);
                script = sb.ToArray();
            }
            ApplicationEngine engine = ApplicationEngine.Run(script, testMode: true);

            LogEngine(engine);
            return true;
        }
        private bool OnInvoke(string[] parameters)
        {
            if (parameters.Length < 3) return false;
            UInt160 hash = UInt160.Parse(parameters[1]);
            string method = parameters[2];
            object[] args = parameters.Length > 2 ? parameters.Skip(3).ToArray() : new object[0];

            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitAppCall(hash, method, args);
                script = sb.ToArray();
            }
            InvocationTransaction tx = new InvocationTransaction();
            tx.Version = 1;
            tx.Script = script;
            if (tx.Attributes == null) tx.Attributes = new TransactionAttribute[0];
            if (tx.Inputs == null) tx.Inputs = new CoinReference[0];
            if (tx.Outputs == null) tx.Outputs = new TransactionOutput[0];
            if (tx.Witnesses == null) tx.Witnesses = new Witness[0];

            ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, testMode: true);

            LogEngine(engine);

            if (engine.State.HasFlag(VMState.FAULT))
            {
                Console.WriteLine("Execution Failed");
                return true;
            }

            tx.Gas = engine.GasConsumed - Fixed8.FromDecimal(10);
            if (tx.Gas < Fixed8.Zero) tx.Gas = Fixed8.Zero;
            tx.Gas = tx.Gas.Ceiling();
            Fixed8 fee = tx.Gas.Equals(Fixed8.Zero) ? net_fee : tx.Gas;

            tx = wallet.MakeTransaction(tx, fee: fee);
            ContractParametersContext context = new ContractParametersContext(tx);
            wallet.Sign(context);
            wallet.ApplyTransaction(tx);

            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                wallet.ApplyTransaction(tx);
                system.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                Console.WriteLine($"Relayed Transaction: {tx.ToJson()}");
            }
            else
            {
                Console.WriteLine(context.ToJson());
            }
            return true;
        }
        private bool OnTestInvoke(string[] parameters)
        {
            if (parameters.Length < 4) return false;
            UInt160 hash = UInt160.Parse(parameters[2]);
            string method = parameters[3];
            object[] args = parameters.Length > 3 ? parameters.Skip(4).ToArray() : new object[0];

            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitAppCall(hash, method, args);
                script = sb.ToArray();
            }
            ApplicationEngine engine = ApplicationEngine.Run(script, testMode: true);

            LogEngine(engine);
            return true;
        }

        private bool OnHelp(string[] args)
        {
            if (args.Length < 2) return false;
            if (!string.Equals(args[1], Name, StringComparison.OrdinalIgnoreCase))
                return false;
            Console.Write($"{Name} Commands:\n" + "\tcompile <path>\n"
                + "\tdeploy <path>\n" + "\ttest deploy <path>\n"
                + "\tinvoke <hash>\n" + "\ttest invoke <hash>\n");
            return true;
        }

        private void LogEngine(ApplicationEngine engine)
        {
            Console.WriteLine($"VM State: {engine.State}");
            Console.WriteLine($"Gas Consumed: {engine.GasConsumed}");
            Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
        }
    }
}
