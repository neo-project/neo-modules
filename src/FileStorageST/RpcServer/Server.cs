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

        public Server(int port, string sslCert, string sslCertPassword)
        {
            this.port = port;
            this.sslCert = sslCert;
            this.sslCertPassword = sslCertPassword;
            hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureLogging(logBuilder =>
                {
                    logBuilder.ClearProviders()
                        .AddProvider(new LoggerProvider());
                });
        }

        public void BindService<T>(T serviceImpl)
        {
            startup.InjectSingletonService(typeof(T), serviceImpl);
        }

        public void Start()
        {
            hostBuilder.ConfigureWebHostDefaults(webBuilder =>
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
            })
            .Build()
            .RunAsync(cancellationSource.Token);
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
