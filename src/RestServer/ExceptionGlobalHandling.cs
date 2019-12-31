using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using System;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public class ExceptionGlobalHandling
    {
        private readonly RequestDelegate next;

        public ExceptionGlobalHandling(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await next(httpContext);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            JObject response = new JObject();
            response["error"] = new JObject();
            response["error"]["code"] = exception.HResult;
            response["error"]["message"] = exception.Message;
            return context.Response.WriteAsync(response.ToString());
        }
    }
}
