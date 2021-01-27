using Akka.Actor;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.FSStorage;

namespace FSStorageTests.innering.processors
{
    public class ProcessorFakeActor : ReceiveActor
    {
        public ProcessorFakeActor()
        {
            Receive<Transaction>(create =>
            {
                Sender.Tell(new OperationResult1() { tx = create });
            });
            Receive<Neo.Plugins.util.WorkerPool.NewTask>(create =>
            {
                Sender.Tell(new OperationResult2() { nt = create });
            });
            Receive<IContractEvent>(create =>
            {
                Sender.Tell(new OperationResult3() { ce = create });
            });
        }

        public class OperationResult1 { public Transaction tx; };
        public class OperationResult2 { public Neo.Plugins.util.WorkerPool.NewTask nt; };
        public class OperationResult3 { public IContractEvent ce; };
    }
}
