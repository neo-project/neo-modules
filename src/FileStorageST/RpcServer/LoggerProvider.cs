using Microsoft.Extensions.Logging;

namespace Neo.FileStorage.Storage.RpcServer
{
    public class LoggerProvider : ILoggerProvider
    {
        ILogger ILoggerProvider.CreateLogger(string categoryName)
        {
            return new Logger(categoryName);
        }

        public void Dispose() { }
    }
}
