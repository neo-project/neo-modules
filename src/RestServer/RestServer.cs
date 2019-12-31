using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
            RestSettings.Load(GetConfiguration());
        }

        public class AuthorizeActionFilter : IAuthorizationFilter
        {
            public void OnAuthorization(AuthorizationFilterContext context)
            {
                if (!CheckAuth(context.HttpContext) || RestSettings.Default.DisabledMethods.Contains(context.HttpContext.Request.Path.ToString()))
                    throw new RestException(-400, "Access denied");
            }

            private bool CheckAuth(HttpContext context)
            {
                if (string.IsNullOrEmpty(RestSettings.Default.RpcUser)) return true;

                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Restricted\"";

                string reqauth = context.Request.Headers["Authorization"];
                string authstring;
                try
                {
                    authstring = Encoding.UTF8.GetString(Convert.FromBase64String(reqauth.Replace("Basic ", "").Trim()));
                }
                catch
                {
                    return false;
                }

                string[] authvalues = authstring.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                if (authvalues.Length < 2)
                    return false;

                return authvalues[0] == RestSettings.Default.RpcUser && authvalues[1] == RestSettings.Default.RpcPass;
            }
        }

        protected override void OnPluginsLoaded()
        {
            host = new WebHostBuilder().UseKestrel(options => options.Listen(RestSettings.Default.BindAddress, RestSettings.Default.Port, listenOptions =>
            {
                if (string.IsNullOrEmpty(RestSettings.Default.SslCert)) return;
                listenOptions.UseHttps(RestSettings.Default.SslCert, RestSettings.Default.SslCertPassword, httpsConnectionAdapterOptions =>
                {
                    if (RestSettings.Default.TrustedAuthorities is null || RestSettings.Default.TrustedAuthorities.Length == 0)
                        return;
                    httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    httpsConnectionAdapterOptions.ClientCertificateValidation = (cert, chain, err) =>
                    {
                        if (err != SslPolicyErrors.None)
                            return false;
                        X509Certificate2 authority = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                        return RestSettings.Default.TrustedAuthorities.Contains(authority.Thumbprint);
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
                  services.AddMvcCore(config =>
                  {
                      config.Filters.Add(typeof(AuthorizeActionFilter));
                  }).AddApiExplorer();
                  services.AddSingleton(s => System);
              })
            .Configure(app =>
            {
                app.UseMiddleware<ExceptionGlobalHandling>();
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
