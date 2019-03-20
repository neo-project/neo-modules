using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;

namespace Neo.Plugins
{
    public class CoreMetrics : Plugin, IRpcPlugin
    {
        public override void Configure()
        {
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            switch (method)
            {
                case "getmetricblocktimestamp":
                    {

                        uint nBlocks = (uint)_params[0].AsNumber();
                        uint lastHeight = _params.Count >= 2 ? lastHeight = (uint)_params[1].AsNumber() : 0;
                        return GetBlocksTime(nBlocks, lastHeight);
                    }
                default:
                    return null;
            }
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }

        private JObject GetBlocksTime(uint nBlocks, uint lastHeight)
        {
            // It is currently limited to query blocks generated in the last 24hours (86400 seconds)
            uint maxNBlocksPerDay = 86400 / Blockchain.SecondsPerBlock;
            if (lastHeight != 0)
            {
                if (lastHeight >= Blockchain.Singleton.Height)
                {
                    JObject json = new JObject();
                    return json["error"] = "Requested height to start " + lastHeight + " exceeds " + Blockchain.Singleton.Height;
                }

                if (nBlocks > lastHeight)
                {
                    JObject json = new JObject();
                    return json["error"] = "Requested " + nBlocks + " blocks timestamps is greater than starting at height " + lastHeight;
                }
            }

            if (nBlocks > maxNBlocksPerDay)
            {
                JObject json = new JObject();
                return json["error"] = "Requested number of blocks timestamps exceeds " + maxNBlocksPerDay;
            }

            if (nBlocks >= Blockchain.Singleton.Height)
            {
                JObject json = new JObject();
                return json["error"] = "Requested number of blocks timestamps exceeds quantity of known blocks " + Blockchain.Singleton.Height;
            }

            if (nBlocks <= 0)
            {
                JObject json = new JObject();
                return json["error"] = "Requested number of block times can not be <= 0";
            }

            JArray array = new JArray();
            uint heightToBegin = lastHeight > 0 ? lastHeight - nBlocks : Blockchain.Singleton.Height - nBlocks;
            for (uint i = heightToBegin; i <= Blockchain.Singleton.HeaderHeight; i++)
            {
                JObject json = new JObject();
                Header header = Blockchain.Singleton.Store.GetHeader(i);
                json["timestamp"] = header.Timestamp;
                json["height"] = i;
                array.Add(json);
            }

            return array;
        }
    }
}
