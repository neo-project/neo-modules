// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract.Manifest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class ContractAbiJsonConverter : JsonConverter<ContractAbi>
    {
        public override ContractAbi ReadJson(JsonReader reader, Type objectType, ContractAbi existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, ContractAbi value, JsonSerializer serializer)
        {
            var methodsArrary = new JArray();
            foreach (var method in value.Methods)
            {
                var methodObject = new JObject()
                {
                    ["name"] = method.Name,
                    ["safe"] = method.Safe,
                    ["offset"] = method.Offset,
                };

                var parametersArray = new JArray();
                foreach(var parameter in method.Parameters)
                    parametersArray.Add(new JObject()
                    {
                        ["name"] = parameter.Name,
                        ["type"] = parameter.Type.ToString(),
                    });
                methodObject["parameters"] = parametersArray;

                methodObject["returnType"] = method.ReturnType.ToString();
                methodsArrary.Add(methodObject);
            }

            var eventsArray = new JArray();
            foreach (var ent in value.Events)
            {
                var eventObject = new JObject()
                {
                    ["name"] = ent.Name,
                };

                var parametersArray = new JArray();
                foreach (var parameter in ent.Parameters)
                    parametersArray.Add(new JObject()
                    {
                        ["name"] = parameter.Name,
                        ["type"] = parameter.Type.ToString(),
                    });
                eventObject["parameters"] = parametersArray;

                eventsArray.Add(eventObject);
            }

            var abiObject = new JObject()
            {
                ["methods"] = methodsArrary,
                ["events"] = eventsArray,
            };
            abiObject.WriteTo(writer);
        }
    }
}
