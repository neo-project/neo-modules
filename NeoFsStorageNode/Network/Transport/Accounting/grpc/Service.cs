using Grpc.Core;
using NeoFS.API.v2.Accounting;

namespace Neo.Fs.Network.Transport.Accounting
{
    public class Service
    {
        private AccountingService.AccountingServiceBase srv;

        public Service(AccountingService.AccountingServiceBase service)
        {
            this.srv = service;
        }

        public BalanceResponse Balance(ServerCallContext ctx, BalanceRequest req)
        {
            return srv.Balance(req,ctx).Result;
        }
    }
}
