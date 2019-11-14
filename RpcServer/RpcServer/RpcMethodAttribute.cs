using System;

namespace Neo.Plugins.RpcServer
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RpcMethodAttribute : Attribute
    {
        public string Name { get; set; }
    }
}
