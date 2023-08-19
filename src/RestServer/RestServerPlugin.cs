// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.ConsoleService;
using Neo.Network.P2P;

namespace Neo.Plugins.RestServer
{
    public class RestServerPlugin : Plugin
    {
        public override string Name => "RestServer";
        public override string Description => "Enables REST Web Sevices for the node";

        #region Globals

        private RestServerSettings _settings;
        private RestWebServer _server;

        #endregion

        internal static NeoSystem NeoSystem { get; private set; }
        internal static LocalNode LocalNode { get; private set; }

        protected override void Configure()
        {
            _settings = RestServerSettings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != _settings.Network) return;
            if (_settings.EnableCors && _settings.EnableBasicAuthentication && _settings.AllowCorsUrls.Length == 0)
            {
                ConsoleHelper.Warning("RestServer: CORS is misconfigured!");
                ConsoleHelper.Info($"You have {nameof(_settings.EnableCors)} and {nameof(_settings.EnableBasicAuthentication)} enabled but");
                ConsoleHelper.Info($"{nameof(_settings.AllowCorsUrls)} is empty in config.json for RestServer.");
                ConsoleHelper.Info("You must add urls origins to the list to have CORS work from");
                ConsoleHelper.Info($"browser with authentication enabled.");
                ConsoleHelper.Info($"Example: [\"http://{_settings.BindAddress}:{_settings.Port}\"]");
            }
            NeoSystem = system;
            LocalNode = system.LocalNode.Ask<LocalNode>(new LocalNode.GetInstance()).Result;
            _server = new RestWebServer(_settings);
            _server.Start();
        }
    }
}
