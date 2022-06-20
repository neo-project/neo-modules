using Neo.FileStorage.API.Object;
using Neo.FileStorage.Storage.Placement;
using System;
using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.Storage.Services.Object.Get.Execute
{
    public partial class ExecuteContext
    {
        public CancellationToken Cancellation { get; init; }
        public RangePrm Prm { get; init; }
        public GetService GetService { get; init; }
        public FSRange Range { get; init; }
        public bool HeadOnly { get; init; }
        public ulong CurrentEpoch { get; private set; }
        private FSObject collectedObject;
        private SplitInfo splitInfo = new();
        private Traverser traverser;
        private ulong currentOffset;
        private Exception lastException;

        private bool ShouldWriteHeader => HeadOnly || Range is null;
        private bool ShouldWritePayload => !HeadOnly;
        private bool CanAssemble => GetService.Assemble && !Prm.Raw && !HeadOnly;

        public void Execute()
        {
            try
            {
                ExecuteLocal();
            }
            catch (SplitInfoException se)
            {
                splitInfo.MergeSplitInfo(se.SplitInfo);
                if (CanAssemble)
                    Assemble();
                else
                    throw;
            }
            catch (ObjectNotFoundException)
            {
                if (!Prm.Local)
                {
                    try
                    {
                        ExecuteOnContainer();
                    }
                    catch (SplitInfoException se)
                    {
                        splitInfo.MergeSplitInfo(se.SplitInfo);
                        if (CanAssemble)
                            Assemble();
                        else
                            throw;
                    }
                }
                else
                    throw;
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
                Prm.Writer.WriteHeader(collectedObject.CutPayload());
            }
            return true;
        }

        private bool WriteObjectPayload(FSObject obj)
        {
            if (ShouldWritePayload)
                Prm.Writer.WriteChunk(obj.Payload.ToByteArray());
            return true;
        }
    }
}
