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
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

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

            if (RestServerSettings.Current.EnableBasicAuthentication && CheckHttpBasicAuthentication(request) == false)
            {
                response.Headers.WWWAuthenticate = "Basic realm=\"Restricted\"";
                response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await _next(context);
        }

        public static void SetServerInfomationHeader(HttpResponse response)
        {
            var neoCliAsm = Assembly.GetEntryAssembly().GetName();
            var restServerAsm = Assembly.GetExecutingAssembly().GetName();

            response.Headers.Server = $"{neoCliAsm.Name}/{neoCliAsm.Version.ToString(3)} {restServerAsm.Name}/{restServerAsm.Version.ToString(3)}";
        }

        public bool CheckHttpBasicAuthentication(HttpRequest request)
        {
            var authHeader = request.Headers.Authorization;
            if (AuthenticationHeaderValue.TryParse(authHeader, out AuthenticationHeaderValue authValue))
            {
                if (authValue.Scheme.Equals("basic", StringComparison.OrdinalIgnoreCase) &&
                    authValue.Parameter != null)
                {
                    try
                    {
                        var decodedParams = Encoding.UTF8.GetString(Convert.FromBase64String(authValue.Parameter));
                        var creds = decodedParams.Split(':', 2);
                        return (creds[0] == RestServerSettings.Current.RestUser && creds[1] == RestServerSettings.Current.RestPass);
                    }
                    catch (FormatException)
                    {
                    }
                }
            }
            return false;
        }
    }
}
