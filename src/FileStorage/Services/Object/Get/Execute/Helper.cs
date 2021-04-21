using Google.Protobuf;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Services.Reputaion.Local.Client;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Get.Execute
{
    public static class Helper
    {
        public static FSObject GetObject(this ReputationClient client, ExecuteContext context)
        {
            if (context.HeadOnly)
            {
                return client.GetObjectHeader(new()
                {
                    Address = context.Prm.Address,
                    Raw = context.Prm.Raw,
                }, new()
                {
                    XHeaders = new XHeader[] { new() { Key = XHeader.XHeaderNetmapEpoch, Value = context.CurrentEpoch.ToString() } },
                    Key = context.Prm.Key,
                }).Result;
            }
            if (context.Range is not null)
            {
                var data = client.GetObjectPayloadRangeData(new()
                {
                    Address = context.Prm.Address,
                    Range = context.Range,
                    Raw = context.Prm.Raw,
                }, new()
                {
                    XHeaders = new XHeader[] { new() { Key = XHeader.XHeaderNetmapEpoch, Value = context.CurrentEpoch.ToString() } },
                    Key = context.Prm.Key,
                }).Result;
                return new() { Payload = ByteString.CopyFrom(data) };
            }
            return client.GetObjectHeader(new()
            {
                Address = context.Prm.Address,
                Raw = context.Prm.Raw,
            }, new()
            {
                XHeaders = new XHeader[] { new() { Key = XHeader.XHeaderNetmapEpoch, Value = context.CurrentEpoch.ToString() } },
                Key = context.Prm.Key,
            }).Result;
        }

        public static bool IsChild(this FSObject obj)
        {
            return obj.Parent != null && obj.Parent.Address == obj.Address;
        }
    }
}
