using System;
using System.Collections.Generic;

namespace Neo.Plugins.HttpServer
{
    public abstract class HttpPlugin : Plugin
    {
        public static readonly List<HttpPlugin> HttpPlugins = new List<HttpPlugin>();

        protected HttpPlugin() : base()
        {
            HttpPlugins.Add(this);
        }

        public abstract void ConfigureHttp(HttpServer server);
    }
}
