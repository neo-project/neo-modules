using Google.Protobuf;
using V2Object = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.API.Refs;
using static Neo.Helper;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.Services.ObjectManager.Transformer;
using System;

namespace Neo.FileStorage.Services.Object.Put
{
    public abstract class ValidatingTarget : IObjectTarget
    {
        public FormatValidator ObjectValidator;
        protected Checksum checksum;
        protected bool initReceived;
        protected byte[] payload = Array.Empty<byte>();
        protected PutResult putResult;

        public PutResult Result => putResult;

        public virtual void WriteHeader(V2Object init)
        {
            checksum = init.Header.PayloadHash;
            if (!(checksum.Type == ChecksumType.Sha256 || checksum.Type == ChecksumType.Tz))
                throw new InvalidOperationException(nameof(ValidatingTarget) + " unsupported paylaod checksum type " + checksum.Type);
            if (!ObjectValidator.Validate(init))
                throw new FormatException(nameof(ValidatingTarget) + " invalid object");
            initReceived = true;
        }

        public virtual void WriteChunk(byte[] chunk)
        {
            payload = Concat(payload, chunk);
        }

        public virtual AccessIdentifiers Close()
        {
            if (!initReceived) throw new InvalidOperationException(nameof(ValidatingTarget) + " missing init");
            if (!checksum.Verify(ByteString.CopyFrom(payload))) throw new InvalidOperationException(nameof(ValidatingTarget) + " invalid checksum");
            return null;
        }
    }
}
