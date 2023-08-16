namespace Neo.Plugins.RestServer.Models
{
    public class ContractModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public UInt160 Hash { get; set; }
        public ManifestModel Manifest { get; set; }
        public NefFileModel Nef { get; set; }
    }
}
