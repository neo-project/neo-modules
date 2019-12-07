using System;

namespace Neo.Plugins
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal class RpcMethodAttribute : Attribute
    {
        public string Name { get; set; }
    }
}
