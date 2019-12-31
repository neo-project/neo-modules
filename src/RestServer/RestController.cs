using Microsoft.AspNetCore.Mvc;

namespace Neo.Plugins
{
    [TypeFilter(typeof(RestServer.AuthorizeActionFilter))]
    [Route("api/")]
    [Produces("application/json")]
    public partial class RestController : ControllerBase
    {
        private readonly NeoSystem system;

        public RestController(NeoSystem system)
        {
            this.system = system;
        }
    }
}

