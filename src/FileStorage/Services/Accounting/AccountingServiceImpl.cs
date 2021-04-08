using Google.Protobuf;
using Grpc.Core;
using Neo.FileStorage.API.Accounting;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Morph.Invoker;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Neo.FileStorage.Services.Accounting
{
    public class AccountingServiceImpl : AccountingService.AccountingServiceBase
    {
        private readonly IClient morphClient;
        private readonly ECDsa key;

        public AccountingServiceImpl(ECDsa k, IClient morph)
        {
            morphClient = morph;
            key = k;
        }

        public override Task<BalanceResponse> Balance(BalanceRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                long balance = MorphContractInvoker.InvokeBalanceOf(morphClient, request.Body.OwnerId.ToByteArray());
                var resp = new BalanceResponse
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
                key.SignResponse(resp);
                return resp;
            });
        }
    }
}
