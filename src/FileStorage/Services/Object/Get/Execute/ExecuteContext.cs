using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.ObjectManager.Placement;
using System;
using static Neo.Utility;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;
using Neo.FileStorage.Morph.Invoker;

namespace Neo.FileStorage.Services.Object.Get.Execute
{
    public partial class ExecuteContext
    {
        public GetCommonPrm Prm { get; init; }
        public GetService GetService { get; init; }
        public FSRange Range { get; init; }
        public bool HeadOnly { get; init; }

        public ulong CurrentEpoch { get; private set; }
        private bool assembly;
        private FSObject collectedObject;
        private SplitInfo splitInfo;
        private Traverser traverser;
        private ulong currentOffset;

        private bool ShouldWriteHeader => HeadOnly || Range is null;
        private bool ShouldWritePayload => !HeadOnly;
        private bool CanAssemble => assembly && !Prm.Raw && !HeadOnly;

        public void Execute()
        {
            try
            {
                ExecuteLocal();
            }
            catch (Exception le) // TODO: handle virtual
            {
                Log("GetExecutor", LogLevel.Debug, "local:" + le.Message);
                if (Prm.Local)
                    throw;
                ExecuteOnContainer();
            }
        }

        private void WriteCollectedObject()
        {
            WriteCollectedHeader();
            WriteObjectPayload(collectedObject);
        }

        private bool WriteCollectedHeader()
        {
            if (!ShouldWriteHeader) return true;
            var cutted = collectedObject.CutPayload();
            Prm.Writer.WriteHeader(cutted);
            return true;
        }

        private bool WriteObjectPayload(FSObject obj)
        {
            if (!ShouldWritePayload) return true;
            Prm.Writer.WriteChunk(obj.Payload.ToByteArray());
            return true;
        }

        private Traverser GenerateTraverser(Address address)
        {
            return GetService.TraverserGenerator.GenerateTraverser(address);
        }

        private void InitEpoch()
        {
            CurrentEpoch = Prm.NetmapEpoch;
            if (0 < CurrentEpoch) return;
            CurrentEpoch = MorphContractInvoker.InvokeEpoch(GetService.MorphClient);
        }
    }
}
