using Grpc.Core;
using Neo.FileStorage.API.Accounting;

namespace Neo.FileStorage.Network.Transport.Accounting
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
            return srv.Balance(req, ctx).Result;
        }
    }
}
