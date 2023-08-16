using Microsoft.AspNetCore.Mvc;

namespace Neo.Plugins.Controllers
{
    [Route("/api/v1/sayhello")]
    public class HomeController : ControllerBase
    {
        private readonly NeoSystem _neosystem;

        public HomeController(
            NeoSystem neoSystem)
        {
            _neosystem = neoSystem;
        }

        [HttpGet]
        public IActionResult Get(
            [FromQuery(Name = "name")]
            string name)
        {
            return Ok($"Hello, {name}");
        }

        [HttpGet("test")]
        public IActionResult GetTest()
        {
            return Ok(UInt160.Parse("0x0313bdad26721ae6867df516139d4e52f749f360"));
        }
    }
}
