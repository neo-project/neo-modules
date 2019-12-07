using System;

namespace Neo.Plugins
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RpcMethodAttribute : Attribute
    {
        public string Name { get; set; }
    }
}
