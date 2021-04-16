using Google.Protobuf;
using Neo.FileStorage.API.Client.ObjectParams;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage;
using Neo.FileStorage.Services.ObjectManager.Placement;
using Neo.FileStorage.Services.Object.Get.Writer;
using Neo.FileStorage.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Utility;
using FSClient = Neo.FileStorage.API.Client.Client;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;
using Neo.FileStorage.Morph.Invoker;

namespace Neo.FileStorage.Services.Object.Get
{
    public partial class ExecuteContext
    {
        private void ExecuteOnContainer()
        {
            InitEpoch();
            var depth = Prm.NetmapLookupDepth;
            while (0 < depth)
            {
                ProcessCurrentEpoch();
                depth--;
                currentEpoch--;
            }
        }

        private void ProcessCurrentEpoch()
        {
            traverser = GenerateTraverser(Prm.Address);
            while (true)
            {
                var addrs = traverser.Next();
                if (!addrs.Any())
                {
                    Log(nameof(ExecuteContext), LogLevel.Debug, " no more nodes, abort placement iteration");
                    break;
                }
                foreach (var addr in addrs)
                {
                    if (ProcessNode(addr))
                    {
                        Log(nameof(ExecuteOnContainer), LogLevel.Debug, " completing the operation");
                        break;
                    }
                }
            }
        }
    }
}
