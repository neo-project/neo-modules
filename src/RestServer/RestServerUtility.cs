// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.VM.Types;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Numerics;
using Array = Neo.VM.Types.Array;
using Boolean = Neo.VM.Types.Boolean;
using Buffer = Neo.VM.Types.Buffer;
using Neo.Wallets;
using Neo.SmartContract;
using Neo.Cryptography.ECC;

namespace Neo.Plugins.RestServer
{
    internal static class RestServerUtility
    {
        public static UInt160 ConvertToScriptHash(string address, ProtocolSettings settings)
        {
            if (UInt160.TryParse(address, out var scriptHash))
                return scriptHash;
            return address?.ToScriptHash(settings.AddressVersion);
        }

        public static bool TryConvertToScriptHash(string address, ProtocolSettings settings, out UInt160 scriptHash)
        {
            try
            {
                if (UInt160.TryParse(address, out scriptHash))
                    return true;
                scriptHash = address.ToScriptHash(settings.AddressVersion);
                return true;
            }
            catch
            {
                scriptHash = UInt160.Zero;
                return false;
            }
        }

        public static StackItem StackItemFromJToken(JToken json)
        {
            StackItem s = StackItem.Null;
            var type = (StackItemType)Enum.Parse(typeof(StackItemType), json["type"].Value<string>(), true);
            var value = json["value"];

            switch (type)
            {
                case StackItemType.Struct:
                    var st = new Struct();
                    foreach (var item in (JArray)value)
                        st.Add(StackItemFromJToken(item));
                    s = st;
                    break;
                case StackItemType.Array:
                    var a = new Array();
                    foreach (var item in (JArray)value)
                        a.Add(StackItemFromJToken(item));
                    s = a;
                    break;
                case StackItemType.Map:
                    var m = new Map();
                    foreach (var item in (JArray)value)
                    {
                        var key = (PrimitiveType)StackItemFromJToken(item["Key"]);
                        m[key] = StackItemFromJToken(item["Value"]);
                    }
                    s = m;
                    break;
                case StackItemType.Boolean:
                    s = value.ToObject<bool>() ? StackItem.True : StackItem.False;
                    break;
                case StackItemType.Buffer:
                    s = new Buffer(Convert.FromBase64String(value.ToObject<string>()));
                    break;
                case StackItemType.ByteString:
                    s = new ByteString(Convert.FromBase64String(value.ToObject<string>()));
                    break;
                case StackItemType.Integer:
                    s = value.ToObject<BigInteger>();
                    break;
                case StackItemType.InteropInterface:
                    s = new InteropInterface(Convert.FromBase64String(value.ToObject<string>()));
                    break;
                case StackItemType.Pointer:
                    s = new Pointer(null, value.ToObject<int>());
                    break;
                default:
                    break;
            }
            return s;
        }

        public static JToken StackItemToJToken(StackItem item, IList<(StackItem, JToken)> context)
        {
            JToken o = null;
            switch (item)
            {
                case Struct @struct:
                    if (context is null)
                        context = new List<(StackItem, JToken)>();
                    else
                        (_, o) = context.FirstOrDefault(f => ReferenceEquals(f.Item1, item));
                    if (o is null)
                    {
                        context.Add((item, o));
                        var a = @struct.Select(s => StackItemToJToken(s, context));
                        o = new JObject()
                        {
                            new JProperty("type", StackItemType.Struct.ToString()),
                            new JProperty("value", JArray.FromObject(a)),
                        };
                    }
                    break;
                case Array array:
                    if (context is null)
                        context = new List<(StackItem, JToken)>();
                    else
                        (_, o) = context.FirstOrDefault(f => ReferenceEquals(f.Item1, item));
                    if (o is null)
                    {
                        context.Add((item, o));
                        var a = array.Select(s => StackItemToJToken(s, context));
                        o = new JObject()
                        {
                            new JProperty("type", StackItemType.Array.ToString()),
                            new JProperty("value", JArray.FromObject(a)),
                        };
                    }
                    break;
                case Map map:
                    if (context is null)
                        context = new List<(StackItem, JToken)>();
                    else
                        (_, o) = context.FirstOrDefault(f => ReferenceEquals(f.Item1, item));
                    if (o is null)
                    {
                        context.Add((item, o));
                        var kvp = map.Select(s => new KeyValuePair<JToken, JToken>(StackItemToJToken(s.Key, context), StackItemToJToken(s.Value, context)));
                        o = new JObject()
                        {
                            new JProperty("type", StackItemType.Map.ToString()),
                            new JProperty("value", JArray.FromObject(kvp)),
                        };
                    }
                    break;
                case Boolean:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.Boolean.ToString()),
                        new JProperty("value", item.GetBoolean()),
                    };
                    break;
                case Buffer:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.Buffer.ToString()),
                        new JProperty("value", Convert.ToBase64String(item.GetSpan())),
                    };
                    break;
                case ByteString:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.ByteString.ToString()),
                        new JProperty("value", Convert.ToBase64String(item.GetSpan())),
                    };
                    break;
                case Integer:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.Integer.ToString()),
                        new JProperty("value", item.GetInteger()),
                    };
                    break;
                case InteropInterface:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.InteropInterface.ToString()),
                        new JProperty("value", JToken.Parse(
                            JsonConvert.SerializeObject(
                                item.GetInterface<object>(),
                                RestServerSettings.Default.JsonSerializerSettings
                            )
                        )),
                    };
                    break;
                case Pointer pointer:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.Pointer.ToString()),
                        new JProperty("value", pointer.Position),
                    };
                    break;
                case Null:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.Any.ToString()),
                        new JProperty("value", null),
                    };
                    break;
                default:
                    throw new NotSupportedException($"StackItemType({item.Type}) is not supported to JSON.");
            }
            return o;
        }

        public static ContractParameter ContractParameterFromJToken(JToken token)
        {
            if (token.Type != JTokenType.Object)
                throw new FormatException();

            var obj = (JObject)token;
            var typeProp = obj
                .Properties()
                .SingleOrDefault(a => a.Name.Equals("type", StringComparison.InvariantCultureIgnoreCase));
            var valueProp = obj
                .Properties()
                .SingleOrDefault(a => a.Name.Equals("value", StringComparison.InvariantCultureIgnoreCase));

            if (typeProp == null || valueProp == null)
                throw new FormatException();

            var typeValue = Enum.Parse<ContractParameterType>(typeProp.ToObject<string>());

            switch (typeValue)
            {
                case ContractParameterType.Any:
                    return new ContractParameter(ContractParameterType.Any);
                case ContractParameterType.ByteArray:
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.ByteArray,
                        Value = Convert.FromBase64String(valueProp.ToObject<string>()),
                    };
                case ContractParameterType.Signature:
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Signature,
                        Value = Convert.FromBase64String(valueProp.ToObject<string>()),
                    };
                case ContractParameterType.Boolean:
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Boolean,
                        Value = valueProp.ToObject<bool>(),
                    };
                case ContractParameterType.Integer:
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Integer,
                        Value = BigInteger.Parse(valueProp.ToObject<string>()),
                    };
                case ContractParameterType.String:
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.String,
                        Value = valueProp.ToObject<string>(),
                    };
                case ContractParameterType.Hash160:
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Hash160,
                        Value = UInt160.Parse(valueProp.ToObject<string>()),
                    };
                case ContractParameterType.Hash256:
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Hash256,
                        Value = UInt256.Parse(valueProp.ToObject<string>()),
                    };
                case ContractParameterType.PublicKey:
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.PublicKey,
                        Value = ECPoint.Parse(valueProp.ToObject<string>(), ECCurve.Secp256r1),
                    };
                case ContractParameterType.Array:
                    if (valueProp.Value?.Type != JTokenType.Array)
                        throw new FormatException();
                    var array = valueProp.Value as JArray;
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Array,
                        Value = array.Select(ContractParameterFromJToken).ToList(),
                    };
                case ContractParameterType.Map:
                    if (valueProp.Value?.Type != JTokenType.Array)
                        throw new FormatException();
                    var map = valueProp.Value as JArray;
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Map,
                        Value = map.Select(s =>
                        {
                            if (s.Type != JTokenType.Object)
                                throw new FormatException();
                            var mapProp = valueProp.Value as JObject;
                            var keyProp = mapProp
                                .Properties()
                                .SingleOrDefault(ss => ss.Name.Equals("key", StringComparison.InvariantCultureIgnoreCase));
                            var keyValueProp = mapProp
                                .Properties()
                                .SingleOrDefault(ss => ss.Name.Equals("value", StringComparison.InvariantCultureIgnoreCase));
                            return new KeyValuePair<ContractParameter, ContractParameter>(ContractParameterFromJToken(keyProp.Value), ContractParameterFromJToken(keyValueProp.Value));
                        }).ToList(),
                    };
                default:
                    throw new NotSupportedException($"ContractParameterType({typeValue}) is not supported to JSON.");
            }
        }
    }
}
