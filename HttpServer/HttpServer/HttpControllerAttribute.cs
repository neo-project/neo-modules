using System;

namespace Neo.Plugins.HttpServer
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HttpControllerAttribute : Attribute
    {
        public string Path { get; set; }
    }
}
