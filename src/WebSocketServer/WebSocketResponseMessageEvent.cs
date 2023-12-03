namespace Neo.Plugins
{
    public enum WebSocketResponseMessageEvent : byte
    {
        Block = 0x02,               // Blockchain.Committed
        MemoryPool = 0x03,          // MemoryPool
        Transaction = 0x05,         // Transaction
        Notify = 0x14,              // ApplicationEngine.Notify
        Log = 0x15,                 // ApplicationEngine.Log
        System = 0x16,              // Utility.Log
        Error = 0x19,               // Errors
        Call = 0x20,                // Result from a method call
    }
}
