using Neo.FileStorage.Storage.Services.Session;
using Neo.FileStorage.Storage.Services.Session.Storage;
using System;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        private TokenStore tokenStore;

        public SessionServiceImpl InitializeSession()
        {
            tokenStore = new();
            return new SessionServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        EpochSource = this,
                        SessionService = new()
                        {
                            TokenStore = tokenStore
                        }
                    }
                }
            };
        }
    }
}
