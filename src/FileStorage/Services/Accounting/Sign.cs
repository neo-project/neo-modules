using Grpc.Core;
using Neo.FileStorage.API.Accounting;
using Neo.FileStorage.API.Session;
using UtilSignService = Neo.FileStorage.Services.Util.SignService;

namespace Neo.FileStorage.Services.Accounting
{
    public class SignService
    {
        private UtilSignService signSvc;
        private AccountingService.AccountingServiceBase svc;

        public SignService(byte[] pk, AccountingService.AccountingServiceBase aSvc)
        {
            this.signSvc = new UtilSignService(pk);
            this.svc = aSvc;
        }

        public BalanceResponse Balance(ServerCallContext ctx, BalanceRequest req)
        {
            var resp = this.signSvc.HandleUnaryRequest(req, (req) =>
            {
                return (IResponse)this.svc.Balance((BalanceRequest)req, ctx);
            });
            return (BalanceResponse)resp;
        }
    }
}
