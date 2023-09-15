// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Neo.Json;
using Neo.Network.P2P;
using System.Reflection;

namespace Neo.Plugins
{
    public abstract partial class NeoService : IDisposable
    {
        protected readonly Dictionary<string, Func<JArray, object>> Methods = new();

        protected IWebHost Host;
        protected NeoServiceSettings Settings;
        protected readonly NeoSystem System;
        protected readonly LocalNode LocalNode;

        public NeoService(NeoSystem system, NeoServiceSettings settings)
        {
            this.System = system;
            this.Settings = settings;
            LocalNode = system.LocalNode.Ask<LocalNode>(new LocalNode.GetInstance()).Result;
            RegisterMethods(this);
            Initialize_SmartContract();
        }


        private static JObject CreateErrorResponse(JToken id, int code, string message, JToken data = null)
        {
            JObject response = CreateResponse(id);
            response["error"] = new JObject();
            response["error"]["code"] = code;
            response["error"]["message"] = message;
            if (data != null)
                response["error"]["data"] = data;
            return response;
        }

        private static JObject CreateResponse(JToken id)
        {
            JObject response = new();
            response["jsonrpc"] = "2.0";
            response["id"] = id;
            return response;
        }

        public void Dispose()
        {
            Dispose_SmartContract();
            if (Host == null) return;
            Host.Dispose();
            Host = null;
        }

        public abstract void StartService();

        internal void UpdateSettings(NeoServiceSettings settings)
        {
            this.Settings = settings;
        }

        protected abstract bool CheckAuth(HttpContext context);

        public abstract Task ProcessAsync(HttpContext context);

        protected abstract Task<JObject> ProcessRequestAsync(HttpContext context, JObject request);

        public void RegisterMethods(object handler)
        {
            foreach (MethodInfo method in handler.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                ServiceMethodAttribute attribute = method.GetCustomAttribute<ServiceMethodAttribute>();
                if (attribute is null) continue;
                string name = string.IsNullOrEmpty(attribute.Name) ? method.Name.ToLowerInvariant() : attribute.Name;
                Methods[name] = method.CreateDelegate<Func<JArray, object>>(handler);
            }
        }
    }
}
