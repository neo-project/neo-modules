using System;

namespace Neo.Plugins
{
    internal class RpcException : Exception
    {
        public RpcException(int code, string message) : base(message)
        {
            HResult = code;
        }
    }
}
