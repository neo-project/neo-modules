using System;
using System.Net;
using System.Net.Sockets;

namespace Neo.FSNode.Network.Muxer
{
    public class NetListenerAdapter
    {
        private TcpListener listener; // TBD

        public Socket Accept()
        {
            if (this.listener is null)
                throw new Exception("nothing to accept");
            return this.listener.AcceptSocket();
        }

        public void Close()
        {
            if (this.listener is null)
                return;
            this.listener.Stop();
        }

        // Addr returns the net.Listener's network address.
        public EndPoint Addr()
        {
            if (this.listener is null)
                return null;
            return this.listener.LocalEndpoint;
        }
    }
}
