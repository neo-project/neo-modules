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
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Neo.ConsoleService;
using Neo.Network.P2P;
using Neo.Plugins.Middleware;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Mime;
using System.Reflection;

namespace Neo.Plugins.RestServer
{
    internal class RestWebServer
    {
        #region Globals

        private readonly RestServerSettings _settings;

        private IWebHost _host;

        #endregion

        public static bool IsRunning { get; private set; }

        public RestWebServer(RestServerSettings settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            if (IsRunning) return;

            _host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    // Web server configuration
                    options.AddServerHeader = false;
                    options.Limits.MaxConcurrentConnections = _settings.MaxConcurrentConnections;
                    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(_settings.KeepAliveTimeout);
                    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
                    options.Listen(_settings.BindAddress, unchecked((int)_settings.Port));
                })
                .ConfigureServices(services =>
                {
                    // dependency injection
                    services.AddSingleton(_settings);

                    // Server configuration
                    if (_settings.EnableCors)
                    {
                        if (_settings.AllowCorsUrls.Length == 0)
                            services.AddCors(options =>
                            {
                                options.AddDefaultPolicy(policy =>
                                {
                                    policy.AllowAnyOrigin()
                                    .AllowAnyHeader()
                                    .WithMethods("GET", "POST");
                                    // The CORS specification states that setting origins to "*" (all origins)
                                    // is invalid if the Access-Control-Allow-Credentials header is present.
                                    //.AllowCredentials() 
                                });
                            });
                        else
                            services.AddCors(options =>
                            {
                                options.AddDefaultPolicy(policy =>
                                {
                                    policy.WithOrigins(_settings.AllowCorsUrls)
                                    .AllowAnyHeader()
                                    .AllowCredentials()
                                    .WithMethods("GET", "POST");
                                });
                            });
                    }

                    services.AddRouting(options => options.LowercaseUrls = options.LowercaseQueryStrings = true);
                    services.AddResponseCompression(options =>
                    {
                        options.EnableForHttps = false;
                        options.Providers.Add<GzipCompressionProvider>();
                        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append(MediaTypeNames.Application.Json);
                    });

                    var controllers = services.AddControllers(options => options.EnableEndpointRouting = false);

                    // Load all plugins Controllers
                    foreach (var plugin in Plugin.Plugins)
                        controllers.AddApplicationPart(Assembly.GetAssembly(plugin.GetType()));

                    // Json Binding for http server
                    controllers.AddNewtonsoftJson(options =>
                    {
                        options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                        options.SerializerSettings.Formatting = Formatting.None;

                        foreach (var converter in _settings.JsonSerializerSettings.Converters)
                            options.SerializerSettings.Converters.Add(converter);
                    });

                    // Service configuration
                    services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = ForwardedHeaders.XForwardedFor);
                    services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.SmallestSize);
                })
                .Configure(app =>
                {
#if DEBUG
                    app.UseDeveloperExceptionPage();
#endif
                    app.UseForwardedHeaders();
                    app.UseRouting();
                    app.UseCors();
                    app.UseResponseCompression();
                    app.UseMiddleware<RestServerMiddleware>(_settings);
                    app.UseMvc();
                })
                .Build();
            _host.Start();
            IsRunning = true;
        }
    }
}
