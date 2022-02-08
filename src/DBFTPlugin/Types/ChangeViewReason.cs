// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Consensus.DBFT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Consensus
{
    public enum ChangeViewReason : byte
    {
        Timeout = 0x0,
        ChangeAgreement = 0x1,
        TxNotFound = 0x2,
        TxRejectedByPolicy = 0x3,
        TxInvalid = 0x4,
        BlockRejectedByPolicy = 0x5
    }
}
