namespace Neo.Plugins.RestServer.Models
{
    public class RemoteNodeModel
    {
        public string RemoteAddress { get; set; }
        public int RemotePort { get; set; }
        public int ListenTcpPort { get; set; }
        public uint LastBlockIndex { get; set; }
    }
}
