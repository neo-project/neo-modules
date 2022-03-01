using Neo.FileStorage.Database.LevelDB;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Storage.Services.Session;
using Neo.FileStorage.Storage.Services.Session.Storage;
using System;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        private DB tokenDb;
        private TokenStore tokenStore;

        public SessionServiceImpl InitializeSession()
        {
            tokenDb = new(Settings.Default.GrpcSettings.SessionStorePath);
            tokenStore = new(tokenDb);
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is NewEpochEvent e)
                {
                    tokenStore.RemoveExpired(e.EpochNumber);
                }
            });
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
