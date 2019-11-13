using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.Plugins.RpcServer
{
	public class RpcOperationPayload : IRpcOperationPayload
	{
		public HttpContext Context { get; set; }
		public string ControllerName { get; set; }
		public string OperationName { get; set; }
		public string Id { get; set; }
		public IDictionary<string, object> ParametersDictionary { get; set; }
		public object[] ParametersArray { get; set; }
	}
}
