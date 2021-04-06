using Grpc.Core;
using Neo.FileStorage.Services.Util.Response;
using Neo.FileStorage.API.Accounting;
using Neo.FileStorage.API.Session;
using System.Threading.Tasks;
using Neo.FileStorage.Morph.Invoker;
using Google.Protobuf;

namespace Neo.FileStorage.Services.Accounting
{
    public class AccountingServiceImpl : AccountingService.AccountingServiceBase
    {
        private readonly IClient morphClient;

        public AccountingServiceImpl(IClient morph)
        {
            morphClient = morph;
        }

        public override Task<BalanceResponse> Balance(BalanceRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                long balance = MorphContractInvoker.InvokeBalanceOf(morphClient, request.Body.OwnerId.ToByteArray());
                return new BalanceResponse
                {
                    Body = new BalanceResponse.Types.Body
                    {
                        Balance = new Decimal
                        {
                            Value = balance,
                            Precision = 8,
                        }
                    }
                };
            });
        }
    }
}
