using System;

namespace Neo.Plugins
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal class WebSocketMethodAttribute : Attribute
    {
        public string Name { get; set; }
    }
}
