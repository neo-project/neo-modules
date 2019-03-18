using System.Collections.Generic;
using System.IO;
using Neo.IO;
using Neo.Ledger;

namespace Neo.Plugins
{
        public class UserSystemAssetCoinOutputs : StateBase, ICloneable<UserSystemAssetCoinOutputs>
        {
            public Fixed8 TotalAmount;
            public Dictionary<ushort, Fixed8> AmountByTxIndex;

            public override int Size => base.Size + TotalAmount.Size + sizeof(ushort) +
                                        (AmountByTxIndex.Count * (sizeof(ushort) + sizeof(ulong)));

            public UserSystemAssetCoinOutputs()
            {
                TotalAmount = new Fixed8(0);
                AmountByTxIndex = new Dictionary<ushort, Fixed8>();
            }

            public void AddTxIndex(ushort index, Fixed8 amount)
            {
                TotalAmount += amount;
                AmountByTxIndex.Add(index, amount);
            }

            public bool RemoveTxIndex(ushort index)
            {
                if(AmountByTxIndex.TryGetValue(index, out Fixed8 amount))
                {
                    AmountByTxIndex.Remove(index);
                    TotalAmount -= amount;
                    return true;
                }

                return false;
            }
            public UserSystemAssetCoinOutputs Clone()
            {
                return new UserSystemAssetCoinOutputs()
                {
                    TotalAmount = TotalAmount,
                    AmountByTxIndex = new Dictionary<ushort, Fixed8>(AmountByTxIndex)
                };
            }

            public void FromReplica(UserSystemAssetCoinOutputs replica)
            {
                TotalAmount = replica.TotalAmount;
                AmountByTxIndex = replica.AmountByTxIndex;
            }

            public override void Serialize(BinaryWriter writer)
            {
                base.Serialize(writer);
                writer.Write(TotalAmount);
                writer.Write((ushort)AmountByTxIndex.Count);
                foreach (KeyValuePair<ushort, Fixed8> txIndex in AmountByTxIndex)
                {
                    writer.Write(txIndex.Key);
                    writer.Write(txIndex.Value);
                }
            }

            public override void Deserialize(BinaryReader reader)
            {
                base.Deserialize(reader);
                ((ISerializable)TotalAmount).Deserialize(reader);
                ushort count = reader.ReadUInt16();
                for (int i = 0; i < count; i++)
                {
                    ushort txIndex = reader.ReadUInt16();
                    Fixed8 amount = reader.ReadSerializable<Fixed8>();
                    AmountByTxIndex.Add(txIndex, amount);
                }
            }
        }
}