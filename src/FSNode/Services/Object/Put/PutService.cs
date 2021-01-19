using V2Object = NeoFS.API.v2.Object.Object;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Core.Container;
using Neo.FSNode.Core.Netmap;
using Neo.FSNode.Core.Object;
using Neo.FSNode.Services.Object.Util;
using Neo.FSNode.Services.ObjectManager.Placement;
using Neo.FSNode.Services.ObjectManager.Transformer;
using System;

namespace Neo.FSNode.Services.Object.Put
{
    public class PutService
    {
        private INetmapSource netmapSource;
        private IContainerSource containerSource;
        private IMaxSizeSource maxSizeSource;
        private FormatValidator objectValidator;
        private KeyStorage keyStorage;

        public ObjectID Put(V2Object obj)
        {
            return new ObjectID();
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
            prm.Builder = new NetmapBuilder(new NetmapSource(nm));
        }
    }
}
