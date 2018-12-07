using Akka.Actor;
using Neo.Compiler;
using Neo.Cryptography.ECC;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;


namespace Neo.Plugins
{
    public class SmartContract : Plugin
    {
        private Wallet wallet = null;

        private static readonly Fixed8 net_fee = Fixed8.FromDecimal(0.001m);

        protected override bool OnMessage(object message)
        {
            if (message is Wallet wallet)
            {
                return OnInit(wallet);
            }
            if (message is string[] args)
            {
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
                    return true;
                }
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

        private bool OnInit(Wallet wallet)
        {
            this.wallet = wallet;
            return true;
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

            byte[] new_script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Neo.Contract.Create", script, parameter_list, return_type, properties, values["Name"], values["Version"], values["Author"], values["Email"], values["Description"]);
                new_script = sb.ToArray();
            }
            InvocationTransaction tx = GetTransaction(new_script);

            ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, testMode: true);

            LogEngine(engine);
            Console.WriteLine($"Contract Hash: {script.ToScriptHash().ToString()}");

            if (engine.State.HasFlag(VMState.FAULT))
            {
                Console.WriteLine("Execution Failed");
                return true;
            }

            tx.Gas = engine.GasConsumed - Fixed8.FromDecimal(10);
            if (tx.Gas < Fixed8.Zero) tx.Gas = Fixed8.Zero;
            tx.Gas = tx.Gas.Ceiling();
            Fixed8 fee = tx.Gas.Equals(Fixed8.Zero) ? net_fee : Fixed8.Zero;

            Console.Write("[Confirmation(y/N)]> ");
            if (Console.ReadLine() == "y")
                SendTransaction(tx, fee);
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
                string value = Console.ReadLine();
                values.Add(key, value == null ? "" : value);
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

            byte[] new_script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Neo.Contract.Create", script, parameter_list, return_type, properties, values["Name"], values["Version"], values["Author"], values["Email"], values["Description"]);
                new_script = sb.ToArray();
            }
            InvocationTransaction tx = GetTransaction(new_script);

            ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, testMode: true);

            LogEngine(engine);
            Console.WriteLine($"Contract Hash: {script.ToScriptHash().ToString()}");
            return true;
        }
        private bool OnInvoke(string[] parameters)
        {
            if (parameters.Length < 2)
            {
                Console.WriteLine("error");
                return true;
            }
            if (wallet == null)
            {
                Console.WriteLine("No Wallet Open");
                return true;
            }

            UInt160 hash = UInt160.Parse(parameters[1]);

            Console.Write("[Method]> ");
            string method = Console.ReadLine();

            ContractParameter[] cparams = null;
            try
            {
                cparams = GetParameters(0);
            }
            catch (Exception)
            {
                Console.WriteLine("Parameters Error");
                return true;
            }

            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                if (method == "")
                    sb.EmitAppCall(hash, parameters: cparams);
                else
                    sb.EmitAppCall(hash, method, args: cparams);
                script = sb.ToArray();
            }
            InvocationTransaction tx = GetTransaction(script);

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
            Fixed8 fee = tx.Gas.Equals(Fixed8.Zero) ? net_fee : Fixed8.Zero;

            Console.Write("[Confirmation(y/N)]> ");
            if (Console.ReadLine() == "y")
                SendTransaction(tx, fee);
            return true;
        }
        private bool OnTestInvoke(string[] parameters)
        {
            if (parameters.Length < 3) return false;
            UInt160 hash = UInt160.Parse(parameters[2]);

            Console.Write("[Method]> ");
            string method = Console.ReadLine();

            ContractParameter[] cparams = null;
            try
            {
                cparams = GetParameters(0);
            }
            catch (Exception)
            {
                Console.WriteLine("Parameters Error");
                return true;
            }

            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                if (method == "")
                    sb.EmitAppCall(hash, parameters: cparams);
                else
                    sb.EmitAppCall(hash, method, args: cparams);
                script = sb.ToArray();
            }
            InvocationTransaction tx = GetTransaction(script);

            ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, testMode: true);

            LogEngine(engine);
            return true;
        }

        private ContractParameter[] GetParameters(int depth)
        {
            Console.Write(new String(' ', depth) + "[Parameter Types]> ");
            string[] types = Console.ReadLine().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            Console.Write(new String(' ', depth) + "[Parameters]> ");
            string[] values = Console.ReadLine().Split(' ');

            ContractParameter[] parameters = new ContractParameter[types.Length];

            for (int i = 0; i < types.Length; i++)
            {
                switch (Convert.ToByte(types[i], 16))
                {
                    case (byte)ContractParameterType.Array:
                        parameters[i] = new ContractParameter { Type = ContractParameterType.Array, Value = GetParameters(depth + 1) };
                        break;
                    case (byte)ContractParameterType.Boolean:
                        parameters[i] = new ContractParameter { Type = ContractParameterType.Boolean, Value = Boolean.Parse(values[i]) };
                        break;
                    case (byte)ContractParameterType.ByteArray:
                        parameters[i] = new ContractParameter { Type = ContractParameterType.ByteArray, Value = values[i].HexToBytes() };
                        break;
                    case (byte)ContractParameterType.PublicKey:
                        parameters[i] = new ContractParameter { Type = ContractParameterType.PublicKey, Value = ECPoint.Parse(values[i], ECCurve.Secp256r1) };
                        break;
                    case (byte)ContractParameterType.Hash160:
                        parameters[i] = new ContractParameter { Type = ContractParameterType.Hash160, Value = UInt160.Parse(values[i]) };
                        break;
                    case (byte)ContractParameterType.Hash256:
                        parameters[i] = new ContractParameter { Type = ContractParameterType.Hash256, Value = UInt256.Parse(values[i]) };
                        break;
                    case (byte)ContractParameterType.Integer:
                        if (long.TryParse(values[i], out long num))
                            parameters[i] = new ContractParameter { Type = ContractParameterType.Integer, Value = num };
                        else if (BigInteger.TryParse(values[i].Substring(2), NumberStyles.AllowHexSpecifier, null, out BigInteger bi))
                            parameters[i] = new ContractParameter { Type = ContractParameterType.Integer, Value = bi };
                        break;
                    case (byte)ContractParameterType.String:
                        parameters[i] = new ContractParameter { Type = ContractParameterType.String, Value = values[i] };
                        break;
                    case (byte)ContractParameterType.Void:
                    case (byte)ContractParameterType.InteropInterface:
                    case (byte)ContractParameterType.Map:
                    case (byte)ContractParameterType.Signature:
                    default:
                        throw new FormatException();
                }
            }
            return parameters;
        }

        private InvocationTransaction GetTransaction(byte[] script)
        {
            InvocationTransaction tx = new InvocationTransaction();
            tx.Version = 1;
            tx.Script = script;
            if (tx.Attributes == null) tx.Attributes = new TransactionAttribute[0];
            if (tx.Inputs == null) tx.Inputs = new CoinReference[0];
            if (tx.Outputs == null) tx.Outputs = new TransactionOutput[0];
            if (tx.Witnesses == null) tx.Witnesses = new Witness[0];

            return tx;
        }

        private void SendTransaction(InvocationTransaction tx, Fixed8 fee)
        {
            tx = wallet.MakeTransaction(new InvocationTransaction
            {
                Version = tx.Version,
                Script = tx.Script,
                Gas = tx.Gas,
                Attributes = tx.Attributes,
                Inputs = tx.Inputs,
                Outputs = tx.Outputs
            }, fee: fee);
            if (tx == null)
            {
                Console.WriteLine("Insufficient Funds");
                return;
            }
            ContractParametersContext context;
            try
            {
                context = new ContractParametersContext(tx);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Unsynchronized Block");
                return;
            }

            wallet.Sign(context);

            if (context.Completed)
            {
                context.Verifiable.Witnesses = context.GetWitnesses();
                wallet.ApplyTransaction(tx);
                System.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                Console.WriteLine($"Relayed Transaction: {tx.ToJson()}");
            }
            else
            {
                Console.WriteLine(context.ToJson());
            }
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
