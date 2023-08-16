// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;

namespace Neo.Plugins.RestServer.Models
{
    public class BlockchainExecutionModel
    {
        public TriggerType Trigger { get; set; } = TriggerType.All;
        public VMState VmState { get; set; } = VMState.NONE;
        public string Exception { get; set; } = string.Empty;
        public long GasConsumed { get; set; } = 0L;
        public StackItem[] Stack { get; set; } = System.Array.Empty<StackItem>();
        public BlockchainEventModel[] Notifications { get; set; } = System.Array.Empty<BlockchainEventModel>();
    }
}
