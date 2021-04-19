using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using System;
using System.Security.Cryptography;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSVersion = Neo.FileStorage.API.Refs.Version;
using Neo.FileStorage.Morph.Invoker;

namespace Neo.FileStorage.Services.ObjectManager.Transformer
{
    public class FormatTarget : IObjectTarget
    {
        public ECDsa Key { get; init; }
        public IObjectTarget Next { get; init; }
        public SessionToken SessionToken { get; init; }
        public Client MorphClient { get; init; }

        private FSObject obj;
        private ulong sz;

        public virtual void WriteHeader(FSObject obj)
        {
            this.obj = obj;
        }

        public virtual void WriteChunk(Byte[] chunk) { }

        public AccessIdentifiers Close()
        {
            var curEpoch = CurrentEpoch();

            obj.Header.Version = FSVersion.SDKVersion();
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

            Next.WriteHeader(obj);
            Next.Close();

            return new AccessIdentifiers
            {
                Self = obj.ObjectId,
                Parent = parId
            };
        }

        private ulong CurrentEpoch()
        {
            return MorphContractInvoker.InvokeEpoch(MorphClient);
        }
    }
}
