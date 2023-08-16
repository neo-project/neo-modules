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
