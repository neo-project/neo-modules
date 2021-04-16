using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using System.Security.Cryptography;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.ObjectManager.Transformer
{
    public class FormatterTarget : IObjectTarget
    {
        public ECDsa Key;
        public IObjectTarget NextTarget;
        public SessionToken SessionToken;
        public INetState NetworkState;
        private FSObject obj;
        private ulong sz;

        public virtual void WriteHeader(FSObject obj)
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
            FSObject par;
            if (obj.Header.Split.Parent != null)
            {
                par = new FSObject()
                {
                    ObjectId = obj.Header.Split.Parent,
                    Signature = obj.Header.Split.ParentSignature,
                    Header = obj.Header.Split.ParentHeader
                };
                var rawPar = new FSObject(par);
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
