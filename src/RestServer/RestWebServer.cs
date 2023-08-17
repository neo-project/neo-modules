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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Neo.ConsoleService;
using Neo.Network.P2P;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Concurrent;
using System.Reflection;

namespace Neo.Plugins.RestServer
{
    public partial class RestWebServer
    {
        #region Globals

        private static readonly ConcurrentDictionary<Type, object> _diAddons = new();

        private readonly NeoSystem _neosystem;
        private readonly RestServerSettings _settings;
        private readonly LocalNode _neoLocalNode;

        private IWebHost _host;
        
        #endregion

        public static bool IsRunning { get; private set; }

        public RestWebServer(NeoSystem neoSystem, RestServerSettings settings)
        {
            _neosystem = neoSystem;
            _neoLocalNode = neoSystem.LocalNode.Ask<LocalNode>(new LocalNode.GetInstance()).Result;
            _settings = settings;
        }

        public void Start()
        {
            if (IsRunning) return;

            IsRunning = true;
            _host = new WebHostBuilder().UseKestrel(options =>
                options.Listen(_settings.BindAddress, (int)_settings.Port, listenOptions =>
                {

                }))
                .Configure(app =>
                {
#if DEBUG
                    app.UseDeveloperExceptionPage();
#endif
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

                    foreach (var addon in _diAddons)
                        services.AddSingleton(addon.Key, addon.Value);

                    // Server configuration
                    if (_settings.AllowCors)
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

                        foreach (var converter in _settings.JsonSerializerSettings.Converters)
                            options.SerializerSettings.Converters.Add(converter);
                    });

                    // Service configuration
                    services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = ForwardedHeaders.XForwardedFor);
                })
                .Build();
            _host.Start();
        }

        #region Static Functions

        public static bool AddSingleton<T>(T service) where T : class
        {
            if (IsRunning)
            {
                ConsoleHelper.Error("Some plugins services couldn\'t be loaded correctly.");
                ConsoleHelper.Info("Try increasing StartUpDelay by 1000 in config.json for RestServer");
                ConsoleHelper.Info("and restart node.");
                return false;
            }
            else
                return _diAddons.TryAdd(typeof(T), service);
        }

        #endregion
    }
}
