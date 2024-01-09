// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.ApplicationLogs is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using ApplicationLogs.Store.States;
using Neo.Plugins.Store.Models;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;

namespace ApplicationLogs.Store.Models
{
    public class BlockchainExecutionModel
    {
        public TriggerType Trigger { get; private init; } = TriggerType.All;
        public VMState VmState { get; private init; } = VMState.NONE;
        public string Exception { get; private init; } = string.Empty;
        public long GasConsumed { get; private init; } = 0L;
        public StackItem[] Stack { get; private init; } = System.Array.Empty<StackItem>();
        public BlockchainEventModel[] Notifications { get; set; } = System.Array.Empty<BlockchainEventModel>();
        public ApplicationEngineLogModel[] Logs { get; set; } = System.Array.Empty<ApplicationEngineLogModel>();

        public static BlockchainExecutionModel Create(TriggerType trigger, ExecutionLogState executionLogState, StackItem[] stack) =>
            new()
            {
                Trigger = trigger,
                VmState = executionLogState.VmState,
                Exception = executionLogState.Exception ?? string.Empty,
                GasConsumed = executionLogState.GasConsumed,
                Stack = stack,
            };
    }
}
