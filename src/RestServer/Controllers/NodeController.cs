using Microsoft.AspNetCore.Mvc;
using Neo.IO;
using Neo.Network.P2P;
using Neo.Plugins.RestServer.Models;
using Neo.SmartContract.Native;

namespace Neo.Plugins.Controllers
{
    [Route("/api/v1/node")]
    public class NodeController : ControllerBase
    {
        private readonly LocalNode _neolocalnode;
        private readonly NeoSystem _neosystem;

        public NodeController(
            NeoSystem neoSystem)
        {
            //_neolocalnode = 
            _neosystem = neoSystem;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var rNodes = _neolocalnode
                .GetRemoteNodes()
                .OrderByDescending(o => o.LastBlockIndex)
                .ToArray();

            uint height = NativeContract.Ledger.CurrentIndex(_neosystem.StoreView);
            uint headerHeight = _neosystem.HeaderCache.Last?.Index ?? height;
            int connectedCount = _neolocalnode.ConnectedCount;
            int unconnectedCount = _neolocalnode.UnconnectedCount;

            return Ok(new
            {
                height,
                headerHeight,
                connectedCount,
                unconnectedCount,
                Nodes = rNodes.Select(s =>
                    new RemoteNodeModel()
                    {
                        RemoteAddress = s.Remote.Address.ToString(),
                        RemotePort = s.Remote.Port,
                        ListenTcpPort = s.ListenerTcpPort,
                        LastBlockIndex = s.LastBlockIndex,
                    }),
            });
        }
    }
}
