using System;

namespace Neo.Plugins
{
    public class RestException: Exception
    {
        public RestException(int code, string message) : base(message)
        {
            HResult = code;
        }
    }
}
