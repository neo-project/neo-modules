using System.Collections.Generic;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Metabase
{
    public sealed partial class MB
    {
        public void MoveIt(Address address)
        {
            db.Put(ToMoveItKey(address), ZeroValue);
        }

        public void DoNotMove(Address address)
        {
            db.Delete(ToMoveItKey(address));
        }

        public List<Address> Moveable()
        {
            List<Address> result = new();
            db.Iterate(ToMoveItPrefix, (key, value) =>
            {
                result.Add(new(ContainerID.FromValue(key[1..^32]), ObjectID.FromValue(key[^32..])));
                return false;
            });
            return result;
        }
    }
}
