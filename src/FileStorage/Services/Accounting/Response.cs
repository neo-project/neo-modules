using Grpc.Core;
using Neo.FileStorage.Services.Util.Response;
using Neo.FileStorage.API.Accounting;
using Neo.FileStorage.API.Session;

namespace Neo.FileStorage.Services.Accounting
{
    public class ResponseService
    {
        private Service respSvc;

        private AccountingService.AccountingServiceBase svc;

        public ResponseService(Service rSvc, AccountingService.AccountingServiceBase aSvc)
        {
            this.respSvc = rSvc;
            this.svc = aSvc;
        }

        public BalanceResponse Balance(ServerCallContext ctx, BalanceRequest req)
        {
            var resp = this.respSvc.HandleUnaryRequest(req, (req) =>
            {
                return (IResponse)this.svc.Balance((BalanceRequest)req, ctx);
            });
            return (BalanceResponse)resp;
        }
    }


}
