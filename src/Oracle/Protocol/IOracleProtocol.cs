namespace Neo.Plugins
{
    interface IOracleProtocol
    {
        byte[] Request(ulong requestId, string url, string filter);
    }
}
