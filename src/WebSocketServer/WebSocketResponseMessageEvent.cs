// Copyright (C) 2015-2024 The Neo Project.
//
// WebSocketResponseMessageEvent.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins
{
    public enum WebSocketResponseMessageEvent : byte
    {
        Block = 0x02,               // Blockchain.Committed
        MemoryPool = 0x03,          // MemoryPool
        Transaction = 0x05,         // Transaction
        ContractNotify = 0x14,      // ApplicationEngine.Notify
        AppLog = 0x15,              // ApplicationEngine.Log
        DebugLog = 0x16,            // Utility.Log
        Error = 0x19,               // Error(s)
        Method = 0x20,              // Result(s) from a method call
    }
}
