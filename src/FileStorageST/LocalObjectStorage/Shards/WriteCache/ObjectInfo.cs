using Google.Protobuf;
using Neo.FileStorage.API.Refs;
using System;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public class ObjectInfo
    {
        public FSObject Object;
        public Address Address => Object.Address;
        private string sAddress = "";
        public string SAddress
        {
            get
            {
                if (sAddress == "")
                    sAddress = Object.Address.String();
                return sAddress;
            }
        }
        private byte[] data = null;
        public byte[] Data
        {
            get
            {
                if (data is null)
                    data = Object.ToByteArray();
                return data;
            }
        }
    }
}
