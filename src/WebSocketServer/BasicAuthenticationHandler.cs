using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    internal class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authHeader = Request.Headers.Authorization;
            if (string.IsNullOrEmpty(authHeader) == false && AuthenticationHeaderValue.TryParse(authHeader, out var authValue))
            {
                if (authValue.Scheme.Equals("basic", StringComparison.OrdinalIgnoreCase) && authValue.Parameter != null)
                {
                    try
                    {
                        var decodedParams = Encoding.UTF8.GetString(Convert.FromBase64String(authValue.Parameter));
                        var creds = decodedParams.Split(':', 2);
                        if (creds[0] == WebSocketServerSettings.Current.User && creds[1] == WebSocketServerSettings.Current.Pass)
                        {
                            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, creds[0]) };
                            var identity = new ClaimsIdentity(claims, Scheme.Name);
                            var principal = new ClaimsPrincipal(identity);
                            var ticket = new AuthenticationTicket(principal, Scheme.Name);

                            return Task.FromResult(AuthenticateResult.Success(ticket));
                        }

                    }
                    catch (FormatException)
                    {
                    }
                }
            }
            return Task.FromResult(AuthenticateResult.Fail("Authentication Failed!!!"));
        }
    }
}
