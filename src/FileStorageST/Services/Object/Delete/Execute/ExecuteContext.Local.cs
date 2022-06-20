using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.Services.Object.Delete.Execute
{
    public partial class ExecuteContext
    {
        private void ExecuteLocal()
        {
            FromTombstone();
            SaveTomestone();
            BroadcastTomstone();
        }

        private void FromTombstone()
        {
            tombstone = new()
            {
                ExpirationEpoch = DeleteService.EpochSource.CurrentEpoch + TombstoneLifeTime,
            };
            AddMembers(new() { Prm.Address.ObjectId });
            FromSplitInfo();
            if (SplitInfo is not null)
            {
                tombstone.SplitId = SplitInfo.SplitId;
                CollectMembers();
            }
            InitTombstoneObject();
        }

        private void SaveTomestone()
        {
            var id = DeleteService.PutService.Put(this, false);
            Prm.Writer.SetAddress(new()
            {
                ContainerId = Prm.Address.ContainerId,
                ObjectId = id,
            });
        }

        private void BroadcastTomstone()
        {
            _ = DeleteService.PutService.Put(this, true);
        }

        private void InitTombstoneObject()
        {
            var payload = tombstone.ToByteString();
            TombstoneObject = new();
            TombstoneObject.Header = new();
            TombstoneObject.Header.ContainerId = Prm.Address.ContainerId;
            TombstoneObject.Header.OwnerId = Prm.SessionToken?.Body?.OwnerId;
            if (TombstoneObject.Header.OwnerId is null)
            {
                TombstoneObject.Header.OwnerId = OwnerID.FromPublicKey(DeleteService.LocalInfo.PublicKey);
            }
            TombstoneObject.Header.ObjectType = API.Object.ObjectType.Tombstone;
            TombstoneObject.Payload = payload;
            TombstoneObject.Header.Attributes.Add(new API.Object.Header.Types.Attribute() { Key = API.Object.Header.Types.Attribute.SysAttributeExpEpoch, Value = tombstone.ExpirationEpoch.ToString() });
        }
    }
}
