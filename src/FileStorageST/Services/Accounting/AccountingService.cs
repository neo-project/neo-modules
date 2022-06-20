using Neo.FileStorage.API.Accounting;
using Neo.FileStorage.Invoker.Morph;
using Neo.IO;
using static Neo.FileStorage.Storage.Helper;

namespace Neo.FileStorage.Storage.Services.Accounting
{
    public class AccountingService
    {
        public const int Fixed8Precision = 8;
        public MorphInvoker MorphInvoker { get; init; }

        public BalanceResponse Balance(BalanceRequest request)
        {
            var owner = request.Body?.OwnerId;
            OwnerIDCheck(owner);
            var balance = MorphInvoker.BalanceOf(owner.ToScriptHash());
            var decimals = MorphInvoker.BalanceDecimals();
            var resp = new BalanceResponse
            {
                Body = new BalanceResponse.Types.Body
                {
                    Balance = new Decimal
                    {
                        Value = balance,
                        Precision = decimals,
                    }
                }
            };
            return resp;
        }
    }
}
