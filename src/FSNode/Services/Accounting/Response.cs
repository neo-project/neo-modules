using Grpc.Core;
using Neo.Fs.Services.Util.Response;
using NeoFS.API.v2.Accounting;
using NeoFS.API.v2.Session;

namespace Neo.Fs.Services.Accounting
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
