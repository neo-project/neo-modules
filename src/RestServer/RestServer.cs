using Akka.Actor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Neo.Network.P2P;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace Neo.Plugins.RestServer
{
    public partial class RestServer
    {
        #region Globals

        private readonly NeoSystem _neosystem;
        private readonly RestServerSettings _settings;
        private readonly LocalNode _neoLocalNode;

        private IWebHost _host;

        #endregion

        public RestServer(NeoSystem neoSystem, RestServerSettings settings)
        {
            _neosystem = neoSystem;
            _neoLocalNode = neoSystem.LocalNode.Ask<LocalNode>(new LocalNode.GetInstance()).Result;
            _settings = settings;
        }

        public void StartRestServer()
        {
            _host = new WebHostBuilder().UseKestrel(options =>
                options.Listen(_settings.BindAddress, (int)_settings.Port, listenOptions =>
                {

                }))
                .Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.UseRouting();
                    app.UseCors();
                    app.UseMvc(); // need this to map controllers
                })
                .ConfigureServices(services =>
                {
                    // dependency injection
                    services.AddSingleton(_neosystem);
                    services.AddSingleton(_neoLocalNode);
                    services.AddSingleton(_settings);

                    // Server configuration
                    services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().WithMethods("GET", "POST")));
                    services.AddRouting(options => options.LowercaseUrls = options.LowercaseQueryStrings = true);
                    var controllers = services.AddControllers(options => options.EnableEndpointRouting = false);//.AddApplicationPart(Assembly.GetAssembly(typeof(HomeController)));

                    // Load all plugins Controllers
                    foreach (var plugin in Plugin.Plugins)
                        controllers.AddApplicationPart(Assembly.GetAssembly(plugin.GetType()));

                    controllers.AddNewtonsoftJson(options =>
                    {
                        options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                        options.SerializerSettings.Formatting = Formatting.None;

                        foreach(var converter in _settings.JsonSerializerSettings.Converters)
                            options.SerializerSettings.Converters.Add(converter);
                    });

                    // Service configuration
                    services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = ForwardedHeaders.XForwardedFor);
                })
                .Build();
            _host.Start();
        }
    }
}
