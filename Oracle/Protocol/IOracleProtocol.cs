namespace Oracle.Protocol
{
    interface IOracleProtocol
    {
        byte[] Request(ulong requestId, string url, string filter);
    }
}
