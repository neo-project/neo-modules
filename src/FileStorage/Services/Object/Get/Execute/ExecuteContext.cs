using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.LocalObjectStorage;
using Neo.FileStorage.Services.ObjectManager.Placement;
using System;
using static Neo.Utility;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;


namespace Neo.FileStorage.Services.Object.Get.Execute
{
    public partial class ExecuteContext
    {
        public RangePrm Prm { get; init; }
        public GetService GetService { get; init; }
        public FSRange Range { get; init; }
        public bool HeadOnly { get; init; }

        public ulong CurrentEpoch { get; private set; }
        private FSObject collectedObject;
        private SplitInfo splitInfo;
        private Traverser traverser;
        private ulong currentOffset;

        private bool ShouldWriteHeader => HeadOnly || Range is null;
        private bool ShouldWritePayload => !HeadOnly;
        private bool CanAssemble => GetService.Assemble && !Prm.Raw && !HeadOnly;

        public void Execute()
        {
            try
            {
                ExecuteLocal();
            }
            catch (Exception e)
            {
                Log("GetExecutor", LogLevel.Debug, $"local error, type={e.GetType()}, message={e.Message}");
                if (e is ObjectAlreadyRemovedException || e is RangeOutOfBoundsException)
                    return;
                else if (e is SplitInfoException sie)
                {
                    splitInfo = sie.SplitInfo;
                    Assemble();
                }
                else if (Prm.Local)
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
            if (ShouldWriteHeader)
            {
                var cutted = collectedObject.CutPayload();
                Prm.Writer.WriteHeader(cutted);
            }
            return true;
        }

        private bool WriteObjectPayload(FSObject obj)
        {
            if (ShouldWritePayload)
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
