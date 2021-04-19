using Google.Protobuf;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.Services.ObjectManager.Transformer;
using Neo.FileStorage.API.Refs;
using System;
using static Neo.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Put
{
    public class LocalTarget : IObjectTarget
    {

        public void WriteHeader(FSObject header)
        {
            throw new NotImplementedException();
        }

        public void WriteChunk(byte[] chunk)
        {
            throw new NotImplementedException();
        }

        public AccessIdentifiers Close()
        {
            throw new NotImplementedException();
        }
    }
}
