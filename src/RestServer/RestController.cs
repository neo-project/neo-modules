using Microsoft.AspNetCore.Mvc;

namespace Neo.Plugins
{
    [Route("api/")]
    [Produces("application/json")]
    [ApiController]
    public partial class RestController : Controller
    {
        private NeoSystem system;
        public static Settings settings;

        public RestController(NeoSystem system)
        {
            this.system = system;
        }
    }
}
