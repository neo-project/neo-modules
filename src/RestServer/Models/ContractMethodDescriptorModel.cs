using Neo.SmartContract;

namespace Neo.Plugins.RestServer.Models
{
    public class ContractMethodDescriptorModel
    {
        public string Name { get; set; }
        public bool Safe { get; set; }
        public int Offset { get; set; }
        public IEnumerable<ContractParameterDefinitionModel> Parameters { get; set; }
        public ContractParameterType ReturnType { get; set; }
    }
}
