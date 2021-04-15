using V2Object = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Core.Container;
using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.ObjectManager.Placement;
using Neo.FileStorage.Services.ObjectManager.Transformer;
using System;

namespace Neo.FileStorage.Services.Object.Put
{
    public class PutService
    {
        private INetmapSource netmapSource;
        private IContainerSource containerSource;
        private IMaxSizeSource maxSizeSource;
        private ObjectValidator objectValidator;
        private KeyStorage keyStorage;

        public PutStream Put()
        {
            return new();
        }

        public IObjectTarget Init(PutInitPrm prm)
        {
            var target = InitTarget(prm);
            target.WriteHeader(prm.Init);
            return target;
        }

        private IObjectTarget InitTarget(PutInitPrm prm)
        {
            PreparePrm(prm);
            var session_token = prm.SessionToken;
            if (session_token is null)
            {
                return new DistributeTarget
                {
                    ObjectValidator = objectValidator,
                    Prm = prm,
                };
            }
            var session_key = keyStorage.GetKey(session_token);
            if (session_key is null)
                throw new InvalidOperationException(nameof(InitTarget) + " could not get session key");
            var max_size = maxSizeSource.MaxObjectSize();
            if (max_size == 0)
                throw new InvalidOperationException(nameof(InitTarget) + " could not obtain max object size parameter");
            return new PayloadSizeLimiterTarget(max_size);
        }

        private void PreparePrm(PutInitPrm prm)
        {
            var nm = netmapSource.GetLatestNetworkMap();
            if (nm is null)
                throw new InvalidOperationException(nameof(PreparePrm) + " could not get latest netmap");
            var container = containerSource.Get(prm.Init.Header.ContainerId);
            if (container is null)
                throw new InvalidOperationException(nameof(PreparePrm) + " could not get container by cid");
            prm.Container = container;
            prm.Builder = new NetworkMapBuilder(new NetworkMapSource(nm));
        }
    }
}
