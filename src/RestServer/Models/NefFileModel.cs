namespace Neo.Plugins.RestServer.Models
{
    public class NefFileModel
    {
        public uint CheckSum { get; set; }
        public string Compiler { get; set; }
        public ReadOnlyMemory<byte> Script { get; set; }
        public string Source { get; set; }
        public IEnumerable<MethodTokenModel> Tokens { get; set; }
    }
}
