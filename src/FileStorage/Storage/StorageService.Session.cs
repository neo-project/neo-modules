using System;
using Neo.FileStorage.Services.Session;
using Neo.FileStorage.Services.Session.Storage;

namespace Neo.FileStorage
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
