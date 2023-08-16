using Microsoft.AspNetCore.Mvc;
using Neo.Wallets;

namespace Neo.Plugins.Controllers
{
    [Route("/api/v1/utils")]
    public class UtilsController : ControllerBase
    {
        private readonly NeoSystem _neosystem;

        public UtilsController(
            NeoSystem neoSystem)
        {
            _neosystem = neoSystem;
        }

        [HttpGet("{hash:required}/address")]
        public IActionResult ScriptHashToWalletAddress(
            [FromRoute(Name = "hash")]
            string hash)
        {
            if (UInt160.TryParse(hash, out var scripthash) == false) return BadRequest(nameof(hash));
            return Ok(new { WalletAddress = scripthash.ToAddress(_neosystem.Settings.AddressVersion) });
        }

        [HttpGet("{address:required}/scripthash")]
        public IActionResult WalletAddressToScriptHash(
            [FromRoute(Name = "address")]
            string addr)
        {
            try
            {
                return Ok(new { ScriptHash = addr.ToScriptHash(_neosystem.Settings.AddressVersion) });
            }
            catch
            {
                return BadRequest(nameof(addr));
            }
        }
    }
}
