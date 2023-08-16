// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.RestServer
{
    public class RestServerPlugin : Plugin
    {
        public override string Name => "RestServer";
        public override string Description => "Enables REST Web Sevices for the node";

        #region Globals

        private RestServerSettings _settings;
        private RestServer _server;

        #endregion

        protected override void Configure()
        {
            _settings = RestServerSettings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != _settings.Network) return;
            _server = new RestServer(system, _settings);
            _server.StartRestServer();
        }
    }
}
