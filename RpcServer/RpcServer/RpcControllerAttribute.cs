using System;

namespace Neo.Plugins.RpcServer
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RpcControllerAttribute : Attribute
    {
        public string Name { get; set; }
    }
}
