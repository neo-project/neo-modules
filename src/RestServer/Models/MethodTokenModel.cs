using Neo.SmartContract;

namespace Neo.Plugins.RestServer.Models
{
    public class MethodTokenModel
    {
        public UInt160 Hash { get; set; }
        public string Method { get; set; }
        public ushort ParametersCount { get; set; }
        public bool HasReturnValue { get; set; }
        public CallFlags CallFlags { get; set; }
    }
}
