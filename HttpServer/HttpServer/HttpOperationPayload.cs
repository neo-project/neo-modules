using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.Plugins.HttpServer
{
    public class HttpOperationPayload : IHttpOperationPayload
    {
        public HttpContext Context { get; set; }
		public string Body { get; set; } = "";
		public bool AbortRequest { get; set; } = false;
		public IDictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
		public object Response { get; set; } = new JObject();
	}
}
