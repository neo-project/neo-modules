using Grpc.Core;
using Multiformats.Address;
using System;
using System.Net.Sockets;

namespace Neo.Fs.Network.Muxer
{
    public class Params
    {
        public Server API { get; set; }
        public Multiaddress Address { get; set; }
        public TimeSpan ShutdownTTL { get; set; }
        public Server P2P { get; set; }
    }


    public class Muxer
    {
        private Multiaddress maddr;
        private Int32 run;
        private TcpListener lis;
        private TimeSpan ttl;

        private Server p2p;
        private Server api;

        public void Start()
        {
            // if already started - ignore
            //if (this.run != 0)
            //    return;
            //else if (this.lis != null)
            //{
            //    this.lis.Stop();
            //}

            //this.lis = Helper.Listen(this.maddr);

            //TODO
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
