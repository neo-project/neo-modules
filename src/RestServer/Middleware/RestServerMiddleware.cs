// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace Neo.Plugins.RestServer.Middleware
{
    internal class RestServerMiddleware
    {
        private readonly RequestDelegate _next;

        public RestServerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            var response = context.Response;

            SetServerInfomationHeader(response);

            await _next(context);
        }

        public static void SetServerInfomationHeader(HttpResponse response)
        {
            var neoCliAsm = Assembly.GetEntryAssembly().GetName();
            var restServerAsm = Assembly.GetExecutingAssembly().GetName();

            response.Headers.Server = $"{neoCliAsm.Name}/{neoCliAsm.Version.ToString(3)} {restServerAsm.Name}/{restServerAsm.Version.ToString(3)}";
        }
    }
}
