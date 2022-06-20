using Grpc.Core;
using Neo.FileStorage.API.Accounting;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Status;
using System;
using System.Threading.Tasks;
using static Neo.FileStorage.Storage.Services.Util.Helper;
using APIAccountingService = Neo.FileStorage.API.Accounting.AccountingService;

namespace Neo.FileStorage.Storage.Services.Accounting
{
    public class AccountingServiceImpl : APIAccountingService.AccountingServiceBase
    {
        public AccountingSignService SignService { get; init; }

        public override Task<BalanceResponse> Balance(BalanceRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    return SignService.Balance(request);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(AccountingServiceImpl), LogLevel.Debug, e.Message);
                    if (!IsStatusSupported(request)) throw new RpcException(new(StatusCode.Unknown, e.Message));
                    var resp = new BalanceResponse();
                    resp.SetStatus(e);
                    SignService.Key.Sign(resp);
                    return resp;
                }
            }, context.CancellationToken);
        }
    }
}
