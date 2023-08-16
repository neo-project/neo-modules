namespace Neo.Plugins.RestServer.Models
{
    public class ContractEventDescriptorModel
    {
        public string Name { get; set; }
        public IEnumerable<ContractParameterDefinitionModel> Parameters { get; set; }
    }
}
