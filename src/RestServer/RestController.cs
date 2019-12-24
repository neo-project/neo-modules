using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Neo.Plugins
{
    [Route("api/")]
    [Produces("application/json")]
    [ApiController]
    public partial class RestController : Controller
    {
        private NeoSystem system;
        private Settings settings;

        public RestController(NeoSystem system, IOptions<Settings> settings)
        {
            this.system = system;
            this.settings = settings.Value;
        }
    }
}
