namespace Neo.Plugins.RestServer.Models
{
    public class WitnessModel
    {
        public ReadOnlyMemory<byte> InvocationScript { get; set; }
        public ReadOnlyMemory<byte> VerificationScript { get; set; }
        public UInt160 ScriptHash { get; set; }
    }
}
