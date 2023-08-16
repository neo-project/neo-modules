using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Models
{
    public class ManifestModel
    {
        public string Name { get; set; }
        public ContractAbiModel Abi { get; set; }
        public IEnumerable<ContractGroupModel> Groups { get; set; }
        public IEnumerable<ContractPermissionModel> Permissions { get; set; }
        public IEnumerable<ContractPermissionDescriptorModel> Trusts { get; set; }
        public IEnumerable<string> SupportedStandards { get; set; }
        public JObject Extra { get; set; }
    }
}
