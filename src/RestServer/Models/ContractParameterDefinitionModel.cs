using Neo.SmartContract;

namespace Neo.Plugins.RestServer.Models
{
    public class ContractParameterDefinitionModel
    {
        public string Name { get; set; }
        public ContractParameterType Type { get; set; }
    }
}
