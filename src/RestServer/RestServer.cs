using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Swagger;
using SystemPath=System.IO.Path;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace Neo.Plugins
{
    public sealed class RestServer : Plugin
    {
        private IWebHost host;
        public IConfiguration Configuration;

        public override void Dispose()
        {
            base.Dispose();
            if (host != null)
            {
                host.Dispose();
                host = null;
            }
        }

        protected override void Configure()
        {
            Configuration = GetConfiguration();
        }

        protected override void OnPluginsLoaded()
        {
            host = new WebHostBuilder().UseKestrel(options => options.Listen(IPAddress.Parse(Configuration.GetSection("BindAddress").Value), ushort.Parse(Configuration.GetSection("Port").Value), listenOptions =>
            {
                if (string.IsNullOrEmpty(Configuration["SslCert"])) return;
                listenOptions.UseHttps(Configuration["SslCert"], Configuration["SslCertPassword"], httpsConnectionAdapterOptions =>
                {
                    if (Configuration["TrustedAuthorities"] is null || Configuration["TrustedAuthorities"].Length == 0)
                        return;
                    httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    httpsConnectionAdapterOptions.ClientCertificateValidation = (cert, chain, err) =>
                    {
                        if (err != SslPolicyErrors.None)
                            return false;
                        X509Certificate2 authority = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                        return Configuration["TrustedAuthorities"].Contains(authority.Thumbprint);
                    };
                });
            }))
            .ConfigureServices(services =>
            {
                services.AddSwaggerGen(option =>
                {
                    option.SwaggerDoc("v1", new Info
                    {
                        Title = "Neo Rest API",
                        Version = "v1"
                    });

                    // Set the comments path for the Swagger JSON and UI.
                    option.IncludeXmlComments(SystemPath.Combine(AppContext.BaseDirectory, "Plugins/RestServer/RestServer.xml"), true);
                });

                services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Latest);
                services.AddSingleton(s => System);

                services.AddOptions();
                services.Configure<Settings>(Configuration);
                ///RestController.settings = Settings.Default;
                //services.AddSingleton(s => new RestController(System, settings));
                })
            .Configure(app =>
            {
                //app.UseHttpsRedirection();
                app.UseMiddleware<ExceptionMiddleware>();

                app.UseSwagger();

                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Neo Rest API");
                    c.RoutePrefix = string.Empty;
                });

                app.UseMvc();
            })
            .Build();
            host.Start();
        }
    }
}
