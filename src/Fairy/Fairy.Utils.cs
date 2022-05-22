using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Iterators;
using Neo.VM;
using Neo.VM.Types;

namespace Neo.Plugins
{
    partial class Fairy
    {
        private JObject GetInvokeResult(byte[] script, Signers signers = null)
        {
            Transaction? tx = signers == null ? null : new Transaction
            {
                Signers = signers.GetSigners(),
                Attributes = System.Array.Empty<TransactionAttribute>(),
                Witnesses = signers.Witnesses,
            };
            using ApplicationEngine engine = ApplicationEngine.Run(script, system.StoreView, container: tx, settings: system.Settings, gas: settings.MaxGasInvoke);
            JObject json = new();
            json["script"] = Convert.ToBase64String(script);
            json["state"] = engine.State;
            json["gasconsumed"] = engine.GasConsumed.ToString();
            json["exception"] = GetExceptionMessage(engine.FaultException);
            try
            {
                json["stack"] = new JArray(engine.ResultStack.Select(p => ToJson(p, settings.MaxIteratorResultItems)));
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: invalid operation";
            }
            if (engine.State != VMState.FAULT)
            {
                ProcessInvokeWithWallet(json, signers);
            }
            return json;
        }

        private static JObject ToJson(StackItem item, int max)
        {
            JObject json = item.ToJson();
            if (item is InteropInterface interopInterface && interopInterface.GetInterface<object>() is IIterator iterator)
            {
                JArray array = new();
                while (max > 0 && iterator.Next())
                {
                    array.Add(iterator.Value().ToJson());
                    max--;
                }
                json["iterator"] = array;
                json["truncated"] = iterator.Next();
            }
            return json;
        }

        private static Signers SignersFromJson(JArray _params, ProtocolSettings settings)
        {
            var ret = new Signers(_params.Select(u => new Signer()
            {
                Account = AddressToScriptHash(u["account"].AsString(), settings.AddressVersion),
                Scopes = (WitnessScope)Enum.Parse(typeof(WitnessScope), u["scopes"]?.AsString()),
                AllowedContracts = ((JArray)u["allowedcontracts"])?.Select(p => UInt160.Parse(p.AsString())).ToArray(),
                AllowedGroups = ((JArray)u["allowedgroups"])?.Select(p => ECPoint.Parse(p.AsString(), ECCurve.Secp256r1)).ToArray()
            }).ToArray())
            {
                Witnesses = _params
                    .Select(u => new
                    {
                        Invocation = u["invocation"]?.AsString(),
                        Verification = u["verification"]?.AsString()
                    })
                    .Where(x => x.Invocation != null || x.Verification != null)
                    .Select(x => new Witness()
                    {
                        InvocationScript = Convert.FromBase64String(x.Invocation ?? string.Empty),
                        VerificationScript = Convert.FromBase64String(x.Verification ?? string.Empty)
                    }).ToArray()
            };

            // Validate format

            _ = IO.Helper.ToByteArray(ret.GetSigners()).AsSerializableArray<Signer>();

            return ret;
        }

        static string? GetExceptionMessage(Exception exception)
        {
            return exception?.GetBaseException().Message;
        }
    }
}
