using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace Neo.FileStorage.Storage.RpcServer
{
    public class Server : IDisposable
    {
        private readonly IHostBuilder hostBuilder;
        private readonly CancellationTokenSource cancellationSource = new();
        private readonly Startup startup = new();
        private readonly int port;
        private readonly string sslCert;
        private readonly string sslCertPassword;

        public Server(GrpcSettings settings)
        {
            port = settings.Port;
            sslCert = settings.SslCert;
            sslCertPassword = settings.SslCertPassword;
            hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureLogging(logBuilder =>
                {
                    logBuilder.ClearProviders();
                    if (settings.LogEnabled)
                        logBuilder.AddProvider(new LoggerProvider());
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.ListenAnyIP(port,
                            listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http2;
                                if (string.IsNullOrEmpty(sslCert)) return;
                                listenOptions.UseHttps(sslCert, sslCertPassword);
                            });
                    });
                    webBuilder.UseStartup(cancellationSource => startup);
                });
        }

        public void BindService<T>(T serviceImpl)
        {
            startup.InjectSingletonService(typeof(T), serviceImpl);
        }

        public void Start()
        {
            hostBuilder.Build().Start();
        }

        public void Stop()
        {
            cancellationSource.Cancel();
        }

        public void Dispose()
        {
            cancellationSource.Dispose();
        }
    }
}
