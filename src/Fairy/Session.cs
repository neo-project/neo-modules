using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace Neo.Plugins
{
    class Session : IDisposable
    {
        public ApplicationEngine Engine;
        public Debugger Debugger;
        public SnapshotCache Snapshot;

        public Session(NeoSystem system, byte[] script, Signers? signers = null, ulong timestamp = 0, long gas = ApplicationEngine.TestModeGas, Diagnostic? diagnostic = null)
        {
            Random random = new();
            Snapshot = system.GetSnapshot();
            Transaction? tx = signers == null ? null : new Transaction
            {
                Version = 0,
                Nonce = (uint)random.Next(),
                ValidUntilBlock = NativeContract.Ledger.CurrentIndex(Snapshot) + system.Settings.MaxTraceableBlocks,
                SystemFee = gas,
                NetworkFee = 1_00000000,
                Signers = signers.GetSigners(),
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = script,
                Witnesses = signers.Witnesses
            };
            Engine = ApplicationEngine.Create(TriggerType.Application, tx, Snapshot, Utilities.CreateDummyBlockWithTimestamp(Snapshot, system.Settings, timestamp), system.Settings, gas, diagnostic);
            Debugger = new(Engine);
        }

        public void Dispose()
        {
            Engine.Dispose();
            Snapshot.Dispose();
        }
    }
}
