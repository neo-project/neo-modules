using System.Numerics;
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
            var balance = (BigInteger)MorphInvoker.BalanceOf(request.Body.OwnerId.ToScriptHash().ToArray());
            byte decimals = (byte)MorphInvoker.BalanceDecimals();
            if (decimals > Fixed8Precision)
            {
                BigInteger divisor = BigInteger.Pow(10, decimals - Fixed8Precision);
                balance = BigInteger.Divide(balance, divisor);
            }
            else if (decimals < Fixed8Precision)
            {
                BigInteger divisor = BigInteger.Pow(10, Fixed8Precision - decimals);
                balance = balance * divisor;
            }
            var resp = new BalanceResponse
            {
                Body = new BalanceResponse.Types.Body
                {
                    Balance = new Decimal
                    {
                        Value = (long)balance,
                        Precision = Fixed8Precision,
                    }
                }
            };
            return resp;
        }
    }
}
