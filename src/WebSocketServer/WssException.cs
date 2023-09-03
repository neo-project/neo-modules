using System;
namespace Neo.Plugins.WebSocketServer;

public class WssException : Exception
{
    public WssException(int code, string message) : base(message)
    {
        HResult = code;
    }
}
