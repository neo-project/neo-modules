using System;

namespace Neo.Plugins.HttpServer
{
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpMethodAttribute : Attribute
    {
        public string Path { get; set; }
    }
}
