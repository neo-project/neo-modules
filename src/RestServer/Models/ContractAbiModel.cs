namespace Neo.Plugins.RestServer.Models
{
    public class ContractAbiModel
    {
        public IEnumerable<ContractMethodDescriptorModel> Methods { get; set; }
        public IEnumerable<ContractEventDescriptorModel> Events { get; set; }
    }
}
