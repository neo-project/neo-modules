using Neo.FileStorage.API.Refs;
using Neo.IO.Data.LevelDB;
using System.Collections.Generic;

namespace Neo.FileStorage.LocalObjectStorage.MetaBase
{
    public sealed partial class MB
    {
        public void MoveIt(Address address)
        {
            db.Put(WriteOptions.Default, ToMoveItKey(address), ZeroValue);
        }

        public void DoNotMove(Address address)
        {
            db.Delete(WriteOptions.Default, ToMoveItKey(address));
        }

        public List<Address> Moveable()
        {
            List<Address> result = new();
            Iterate(ToMoveItPrefix, (key, value) =>
            {
                result.Add(new(ContainerID.FromSha256Bytes(key[1..^32]), ObjectID.FromSha256Bytes(key[^32..])));
            });
            return result;
        }
    }
}
