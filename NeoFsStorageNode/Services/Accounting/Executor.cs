using NeoFS.API.v2.Accounting;

namespace Neo.Fs.Services.Accounting
{
    public interface IServiceExecutor
    {
        BalanceResponse.Types.Body Balance(BalanceRequest.Types.Body body);
    }

    public class ExecutorSvc
    {
        private IServiceExecutor exec;

        public ExecutorSvc(IServiceExecutor executor)
        {
            this.exec = executor;
        }

        public BalanceResponse Balance(BalanceRequest req)
        {
            var body = this.exec.Balance(req.Body);
            return new BalanceResponse() { Body = body };
        }
    }
}
