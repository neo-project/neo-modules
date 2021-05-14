using System;
using Neo.FileStorage.Services.Accounting;

namespace Neo.FileStorage
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
                        StorageNode = this,
                        AccountingService = new()
                        {
                            MorphClient = morphClient,
                        }
                    }
                }
            };
        }
    }
}
