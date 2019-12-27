using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using SystemPath = System.IO.Path;

namespace Neo.Plugins
{
    public sealed class RestServer : Plugin
    {
        private IWebHost host;

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
            Settings.Load(GetConfiguration());
        }

        protected override void OnPluginsLoaded()
        {
            host = new WebHostBuilder().UseKestrel(options => options.Listen(Settings.Default.BindAddress, Settings.Default.Port, listenOptions =>
            {
                if (string.IsNullOrEmpty(Settings.Default.SslCert)) return;
                listenOptions.UseHttps(Settings.Default.SslCert, Settings.Default.SslCertPassword, httpsConnectionAdapterOptions =>
                {
                    if (Settings.Default.TrustedAuthorities is null || Settings.Default.TrustedAuthorities.Length == 0)
                        return;
                    httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    httpsConnectionAdapterOptions.ClientCertificateValidation = (cert, chain, err) =>
                    {
                        if (err != SslPolicyErrors.None)
                            return false;
                        X509Certificate2 authority = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                        return Settings.Default.TrustedAuthorities.Contains(authority.Thumbprint);
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

                  services.AddMvcCore().AddApiExplorer();
                  services.AddSingleton(s => System);
              })
            .Configure(app =>
            {
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
