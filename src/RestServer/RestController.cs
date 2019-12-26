using Microsoft.AspNetCore.Mvc;

namespace Neo.Plugins
{
    [Route("api/")]
    [Produces("application/json")]
    public partial class RestController : ControllerBase
    {
        private NeoSystem system;

        public RestController(NeoSystem system)
        {
            this.system = system;
        }
    }
}

