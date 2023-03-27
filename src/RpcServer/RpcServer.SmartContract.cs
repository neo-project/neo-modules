// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        private readonly Dictionary<Guid, Session> _sessions = new();
        private Timer _timer;

        private void Initialize_SmartContract()
        {
            if (_settings.SessionEnabled)
                _timer = new(OnTimer, null, _settings.SessionExpirationTime, _settings.SessionExpirationTime);
        }

        private void Dispose_SmartContract()
        {
            _timer?.Dispose();
            Session[] toBeDestroyed;
            lock (_sessions)
            {
                toBeDestroyed = _sessions.Values.ToArray();
                _sessions.Clear();
            }
            foreach (Session session in toBeDestroyed)
                session.Dispose();
        }

        private void OnTimer(object state)
        {
            List<(Guid Id, Session Session)> toBeDestroyed = new();
            lock (_sessions)
            {
                foreach (var (id, session) in _sessions)
                    if (DateTime.UtcNow >= session.StartTime + _settings.SessionExpirationTime)
                        toBeDestroyed.Add((id, session));
                foreach (var (id, _) in toBeDestroyed)
                    _sessions.Remove(id);
            }
            foreach (var (_, session) in toBeDestroyed)
                session.Dispose();
        }

        private JObject GetInvokeResult(byte[] script, Signer[] signers = null, Witness[] witnesses = null, bool useDiagnostic = false)
        {
            JObject json = new();
            Session session = new(_system, script, signers, witnesses, _settings.MaxGasInvoke, useDiagnostic ? new Diagnostic() : null);
            try
            {
                json["script"] = Convert.ToBase64String(script);
                json["state"] = session.Engine.State;
                json["gasconsumed"] = session.Engine.GasConsumed.ToString();
                json["exception"] = GetExceptionMessage(session.Engine.FaultException);
                json["notifications"] = new JArray(session.Engine.Notifications.Select(n =>
                {
                    var obj = new JObject();
                    obj["eventname"] = n.EventName;
                    obj["contract"] = n.ScriptHash.ToString();
                    obj["state"] = ToJson(n.State, session);
                    return obj;
                }));
                if (useDiagnostic)
                {
                    Diagnostic diagnostic = (Diagnostic)session.Engine.Diagnostic;
                    json["diagnostics"] = new JObject()
                    {
                        ["invokedcontracts"] = ToJson(diagnostic.InvocationTree.Root),
                        ["storagechanges"] = ToJson(session.Engine.Snapshot.GetChangeSet())
                    };
                }
                try
                {
                    json["stack"] = new JArray(session.Engine.ResultStack.Select(p => ToJson(p, session)));
                }
                catch (InvalidOperationException)
                {
                    json["stack"] = "error: invalid operation";
                }
                if (session.Engine.State != VMState.FAULT)
                {
                    ProcessInvokeWithWallet(json, signers);
                }
            }
            catch
            {
                session.Dispose();
                throw;
            }
            if (session.Iterators.Count == 0 || !_settings.SessionEnabled)
            {
                session.Dispose();
            }
            else
            {
                Guid id = Guid.NewGuid();
                json["session"] = id.ToString();
                lock (_sessions)
                    _sessions.Add(id, session);
            }
            return json;
        }

        private static JObject ToJson(TreeNode<UInt160> node)
        {
            JObject json = new();
            json["hash"] = node.Item.ToString();
            if (node.Children.Any())
            {
                json["call"] = new JArray(node.Children.Select(ToJson));
            }
            return json;
        }

        private static JArray ToJson(IEnumerable<DataCache.Trackable> changes)
        {
            JArray array = new();
            foreach (var entry in changes)
            {
                array.Add(new JObject
                {
                    ["state"] = entry.State.ToString(),
                    ["key"] = Convert.ToBase64String(entry.Key.ToArray()),
                    ["value"] = Convert.ToBase64String(entry.Item.Value.ToArray())
                });
            }
            return array;
        }

        private static JObject ToJson(StackItem item, Session session)
        {
            JObject json = item.ToJson();
            if (item is InteropInterface interopInterface && interopInterface.GetInterface<object>() is IIterator iterator)
            {
                Guid id = Guid.NewGuid();
                session.Iterators.Add(id, iterator);
                json["interface"] = nameof(IIterator);
                json["id"] = id.ToString();
            }
            return json;
        }

        private static Signer[] SignersFromJson(JArray @params, ProtocolSettings settings)
        {
            var ret = @params.Select(u => new Signer
            {
                Account = AddressToScriptHash(u["account"].AsString(), settings.AddressVersion),
                Scopes = (WitnessScope)Enum.Parse(typeof(WitnessScope), u["scopes"]?.AsString()),
                AllowedContracts = ((JArray)u["allowedcontracts"])?.Select(p => UInt160.Parse(p.AsString())).ToArray(),
                AllowedGroups = ((JArray)u["allowedgroups"])?.Select(p => ECPoint.Parse(p.AsString(), ECCurve.Secp256r1)).ToArray(),
                Rules = ((JArray)u["rules"])?.Select(r => WitnessRule.FromJson((JObject)r)).ToArray(),
            }).ToArray();

            // Validate format

            _ = IO.Helper.ToByteArray(ret).AsSerializableArray<Signer>();

            return ret;
        }

        private static Witness[] WitnessesFromJson(JArray @params)
        {
            return @params.Select(u => new
            {
                Invocation = u["invocation"]?.AsString(),
                Verification = u["verification"]?.AsString()
            }).Where(x => x.Invocation != null || x.Verification != null).Select(x => new Witness()
            {
                InvocationScript = Convert.FromBase64String(x.Invocation ?? string.Empty),
                VerificationScript = Convert.FromBase64String(x.Verification ?? string.Empty)
            }).ToArray();
        }

        [RpcMethod]
        protected virtual JToken InvokeFunction(JArray @params)
        {
            UInt160 scriptHash = UInt160.Parse(@params[0].AsString());
            string operation = @params[1].AsString();
            ContractParameter[] args = @params.Count >= 3 ? ((JArray)@params[2]).Select(p => ContractParameter.FromJson((JObject)p)).ToArray() : System.Array.Empty<ContractParameter>();
            Signer[] signers = @params.Count >= 4 ? SignersFromJson((JArray)@params[3], _system.Settings) : null;
            Witness[] witnesses = @params.Count >= 4 ? WitnessesFromJson((JArray)@params[3]) : null;
            bool useDiagnostic = @params.Count >= 5 && @params[4].GetBoolean();

            byte[] script;
            using (ScriptBuilder sb = new())
            {
                script = sb.EmitDynamicCall(scriptHash, operation, args).ToArray();
            }
            return GetInvokeResult(script, signers, witnesses, useDiagnostic);
        }

        [RpcMethod]
        protected virtual JToken InvokeScript(JArray @params)
        {
            byte[] script = Convert.FromBase64String(@params[0].AsString());
            Signer[] signers = @params.Count >= 2 ? SignersFromJson((JArray)@params[1], _system.Settings) : null;
            Witness[] witnesses = @params.Count >= 2 ? WitnessesFromJson((JArray)@params[1]) : null;
            bool useDiagnostic = @params.Count >= 3 && @params[2].GetBoolean();
            return GetInvokeResult(script, signers, witnesses, useDiagnostic);
        }

        [RpcMethod]
        protected virtual JToken TraverseIterator(JArray @params)
        {
            Guid sid = Guid.Parse(@params[0].GetString());
            Guid iid = Guid.Parse(@params[1].GetString());
            int count = @params[2].GetInt32();
            if (count > _settings.MaxIteratorResultItems)
                throw new ArgumentOutOfRangeException(nameof(count));
            Session session;
            lock (_sessions)
            {
                session = _sessions[sid];
                session.ResetExpiration();
            }
            IIterator iterator = session.Iterators[iid];
            JArray json = new();
            while (count-- > 0 && iterator.Next())
                json.Add(iterator.Value(null).ToJson());
            return json;
        }

        [RpcMethod]
        protected virtual JToken TerminateSession(JArray @params)
        {
            Guid sid = Guid.Parse(@params[0].GetString());
            Session session;
            bool result;
            lock (_sessions)
                result = _sessions.Remove(sid, out session);
            if (result) session.Dispose();
            return result;
        }

        [RpcMethod]
        protected virtual JToken GetUnclaimedGas(JArray @params)
        {
            string address = @params[0].AsString();
            JObject json = new();
            UInt160 scriptHash;
            try
            {
                scriptHash = AddressToScriptHash(address, _system.Settings.AddressVersion);
            }
            catch
            {
                scriptHash = null;
            }
            if (scriptHash == null)
                throw new RpcException(-100, "Invalid address");
            var snapshot = _system.StoreView;
            json["unclaimed"] = NativeContract.NEO.UnclaimedGas(snapshot, scriptHash, NativeContract.Ledger.CurrentIndex(snapshot) + 1).ToString();
            json["address"] = scriptHash.ToAddress(_system.Settings.AddressVersion);
            return json;
        }

        static string GetExceptionMessage(Exception exception)
        {
            return exception?.GetBaseException().Message;
        }
    }
}
