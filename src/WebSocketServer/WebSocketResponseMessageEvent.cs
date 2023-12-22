namespace Neo.Plugins
{
    public enum WebSocketResponseMessageEvent : byte
    {
        Block = 0x02,               // Blockchain.Committed
        MemoryPool = 0x03,          // MemoryPool
        Transaction = 0x05,         // Transaction
        ContractNotify = 0x14,      // ApplicationEngine.Notify
        AppLog = 0x15,              // ApplicationEngine.Log
        DebugLog = 0x16,            // Utility.Log
        Error = 0x19,               // Error(s)
        Method = 0x20,              // Result(s) from a method call
    }
}
