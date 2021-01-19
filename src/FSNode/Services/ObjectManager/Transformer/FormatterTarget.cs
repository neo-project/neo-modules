using Neo.FSNode.Core.Netmap;
using NeoFS.API.v2.Refs;
using NeoFS.API.v2.Session;
using System.Security.Cryptography;
using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.Services.ObjectManager.Transformer
{
    public class FormatterTarget : IObjectTarget
    {
        public ECDsa Key;
        public IObjectTarget NextTarget;
        public SessionToken SessionToken;
        public IState NetworkState;
        private V2Object obj;
        private ulong sz;

        public virtual void WriteHeader(V2Object obj)
        {
            this.obj = obj;
        }

        public virtual void WriteChunk(byte[] chunk) { }

        public AccessIdentifiers Close()
        {
            var curEpoch = NetworkState.CurrentEpoch();

            obj.Header.Version = Version.SDKVersion();
            obj.Header.PayloadLength = sz;
            obj.Header.SessionToken = SessionToken;
            obj.Header.CreationEpoch = curEpoch;

            ObjectID parId = null;
            V2Object par;
            if (obj.Header.Split.Parent != null)
            {
                par = new V2Object()
                {
                    ObjectId = obj.Header.Split.Parent,
                    Signature = obj.Header.Split.ParentSignature,
                    Header = obj.Header.Split.ParentHeader
                };
                var rawPar = new V2Object(par);
                rawPar.Header.SessionToken = SessionToken;
                rawPar.Header.CreationEpoch = curEpoch;

                var sig = rawPar.CalculateIDSignature(Key); // TBD, 
                rawPar.Signature = sig;
                parId = rawPar.ObjectId;
                obj.Header.Split.Parent = parId;
            }

            var signature = obj.CalculateIDSignature(Key);
            obj.Signature = signature;

            NextTarget.WriteHeader(obj);
            NextTarget.Close();

            return new AccessIdentifiers
            {
                Self = obj.ObjectId,
                Parent = parId
            };
        }
    }
}
