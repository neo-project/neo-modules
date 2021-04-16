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
        private void ExecuteLocal()
        {
            collectedObject = GetService.LocalStorage.Get(Prm.Address);
            WriteCollectedObject();
        }
    }
}
