using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Neo.FileStorage.Storage.gRPC
{
    public class Server
    {
        private readonly IHostBuilder hostBuilder;
        private readonly CancellationTokenSource context = new();
        private readonly Startup startup = new();
        private readonly int port;

        public Server(int port)
        {
            this.port = port;
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
                        listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
                });
                webBuilder.UseStartup(context => startup);
            })
            .Build()
            .RunAsync(context.Token);
        }

        public void Stop()
        {
            context.Cancel();
        }
    }
}
