using Neo.FileStorage.Storage.Services.Accounting;
using System;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        private AccountingServiceImpl InitializeAccounting()
        {
            return new AccountingServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        EpochSource = this,
                        AccountingService = new()
                        {
                            MorphInvoker = morphInvoker,
                        }
                    }
                }
            };
        }
    }
}
