using Google.Protobuf;
using Neo.FileStorage.API.Accounting;
using Neo.FileStorage.Morph.Invoker;

namespace Neo.FileStorage.Services.Accounting
{
    public class AccountingService
    {
        public Client MorphClient { get; init; }

        public BalanceResponse Balance(BalanceRequest request)
        {
            long balance = MorphContractInvoker.InvokeBalanceOf(MorphClient, request.Body.OwnerId.ToByteArray());
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
            return resp;
        }
    }
}
