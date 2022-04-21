// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Consensus.DBFT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
namespace Neo.Consensus;

public enum ConsensusMessageType : byte
{
    ChangeView = 0x00,

    PrepareRequest = 0x20,
    PrepareResponse = 0x21,
    Commit = 0x30,

    RecoveryRequest = 0x40,
    RecoveryMessage = 0x41,
}

public static class Extensions
{
    public static string ToMessage(this ConsensusMessageType type)
    {
        return type switch
        {
            ConsensusMessageType.ChangeView => nameof(ChangeView),
            ConsensusMessageType.PrepareRequest => nameof(PrepareRequest),
            ConsensusMessageType.PrepareResponse => nameof(PrepareResponse),
            ConsensusMessageType.Commit => nameof(Commit),
            ConsensusMessageType.RecoveryRequest => nameof(RecoveryRequest),
            ConsensusMessageType.RecoveryMessage => nameof(RecoveryMessage),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
