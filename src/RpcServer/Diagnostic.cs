// Copyright (C) 2015-2022 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using Neo.VM;

namespace Neo.Plugins
{
    class Diagnostic : IDiagnostic
    {
        public Tree<UInt160> InvocationTree { get; } = new();

        private TreeNode<UInt160> currentNodeOfInvocationTree = null;

        public void Initialized(ApplicationEngine engine)
        {
        }

        public void Disposed()
        {
        }

        public void ContextLoaded(ExecutionContext context)
        {
            var state = context.GetState<ExecutionContextState>();
            if (currentNodeOfInvocationTree is null)
                currentNodeOfInvocationTree = InvocationTree.AddRoot(state.ScriptHash);
            else
                currentNodeOfInvocationTree = currentNodeOfInvocationTree.AddChild(state.ScriptHash);
        }

        public void ContextUnloaded(ExecutionContext context)
        {
            currentNodeOfInvocationTree = currentNodeOfInvocationTree.Parent;
        }

        public void PostExecuteInstruction()
        {
        }

        public void PreExecuteInstruction()
        {
        }
    }
}
