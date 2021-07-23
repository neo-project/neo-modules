using System;
using System.Collections.Generic;
using Neo.Cryptography.ECC;
using Neo.IO;

namespace Neo.FileStorage.Listen.Event.Morph
{
    public class BindEvent : ContractEvent
    {
        public UInt160 UserAccount;
        public ECPoint[] Keys;

        public static BindEvent ParseBindEvent(VM.Types.Array eventParams)
        {
            var bindEvent = new BindEvent();
            if (eventParams.Count != 2) throw new FormatException();
            bindEvent.UserAccount = eventParams[0].GetSpan().AsSerializable<UInt160>();
            List<ECPoint> keys = new();
            var bindKeys = ((VM.Types.Array)eventParams[1]).GetEnumerator();
            while (bindKeys.MoveNext())
            {
                var key = bindKeys.Current.GetSpan().AsSerializable<ECPoint>();
                keys.Add(key);
            }
            bindEvent.Keys = keys.ToArray();
            return bindEvent;
        }
    }

    public class ChequeEvent : ContractEvent
    {
        public byte[] Id;
        public long Amount;
        public UInt160 UserAccount;
        public UInt160 LockAccount;

        public static ChequeEvent ParseChequeEvent(VM.Types.Array eventParams)
        {
            var chequeEvent = new ChequeEvent();
            if (eventParams.Count != 4) throw new FormatException();
            chequeEvent.Id = eventParams[0].GetSpan().ToArray();
            chequeEvent.UserAccount = eventParams[1].GetSpan().AsSerializable<UInt160>();
            chequeEvent.Amount = (long)eventParams[2].GetInteger();
            chequeEvent.LockAccount = eventParams[3].GetSpan().AsSerializable<UInt160>();
            return chequeEvent;
        }
    }

    public class DepositEvent : ContractEvent
    {
        public byte[] Id;
        public long Amount;
        public UInt160 From;
        public UInt160 To;

        public static DepositEvent ParseDepositEvent(VM.Types.Array eventParams)
        {
            var depositEvent = new DepositEvent();
            if (eventParams.Count != 4) throw new FormatException();
            depositEvent.From = eventParams[0].GetSpan().AsSerializable<UInt160>();
            depositEvent.Amount = (long)eventParams[1].GetInteger();
            depositEvent.To = eventParams[2].GetSpan().AsSerializable<UInt160>();
            depositEvent.Id = eventParams[3].GetSpan().ToArray();
            return depositEvent;
        }
    }

    public class WithdrawEvent : ContractEvent
    {
        public byte[] Id;
        public long Amount;
        public UInt160 UserAccount;

        public static WithdrawEvent ParseWithdrawEvent(VM.Types.Array eventParams)
        {
            var withdrawEvent = new WithdrawEvent();
            if (eventParams.Count != 3) throw new FormatException();
            withdrawEvent.UserAccount = eventParams[0].GetSpan().AsSerializable<UInt160>();
            withdrawEvent.Amount = (long)eventParams[1].GetInteger();
            withdrawEvent.Id = eventParams[2].GetSpan().ToArray();
            return withdrawEvent;
        }
    }

    public class ConfigEvent : ContractEvent
    {
        public byte[] Key;
        public byte[] Value;
        public byte[] Id;

        public static ConfigEvent ParseConfigEvent(VM.Types.Array eventParams)
        {
            var configEvent = new ConfigEvent();
            if (eventParams.Count != 3) throw new Exception();
            configEvent.Id = eventParams[0].GetSpan().ToArray();
            configEvent.Key = eventParams[1].GetSpan().ToArray();
            configEvent.Value = eventParams[2].GetSpan().ToArray();
            return configEvent;
        }
    }

    public class UpdateInnerRingEvent : ContractEvent
    {
        public ECPoint[] Keys;

        public static UpdateInnerRingEvent ParseUpdateInnerRingEvent(VM.Types.Array eventParams)
        {
            var updateInnerRingEvent = new UpdateInnerRingEvent();
            if (eventParams.Count != 1) throw new Exception();

            List<ECPoint> keys = new List<ECPoint>();
            var irKeys = ((VM.Types.Array)eventParams[0]).GetEnumerator();
            while (irKeys.MoveNext())
            {
                var key = irKeys.Current.GetSpan().AsSerializable<ECPoint>();
                keys.Add(key);
            }
            updateInnerRingEvent.Keys = keys.ToArray();
            return updateInnerRingEvent;
        }
    }
}
