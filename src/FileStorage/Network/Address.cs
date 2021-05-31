using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Multiformats.Address;
using Multiformats.Address.Net;

namespace Neo.FileStorage.Network
{
    public class Address : IEquatable<Address>
    {
        public const string L4Protocol = "tcp";

        private Multiaddress ma;

        public string String() => ma.ToString();

        public Address() { }

        public Address(Multiaddress m)
        {
            ma = m;
        }

        public string ToIPAddressString()
        {
            return ma.ToEndPoint().ToString();
        }

        public string ToHostAddressString()
        {
            return ToIPAddressString();
        }

        public static Address FromString(string s)
        {
            Multiaddress m;
            try
            {
                m = Multiaddress.Decode(s);
            }
            catch (Exception)
            {
                m = Multiaddress.Decode(MultiAddrStringFromHostAddr(s));
            }
            return new Address(m);
        }

        bool IEquatable<Address>.Equals(Address other)
        {
            if (other is null) return false;
            return other.ma.Equals(ma);
        }

        /// <summary>
        /// multiaddrStringFromHostAddr converts "localhost:8080" to "/dns4/localhost/tcp/8080"
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        private static string MultiAddrStringFromHostAddr(string host)
        {
            if (0 < host.Length && host[0] == ':') host = "0.0.0.0" + host;
            if (host.Last() == ':') host += "0";
            var endPoint = IPEndPoint.Parse(host);
            var addr = endPoint.Address;
            var port = endPoint.Port;
            var prefix = "/dns4";
            var s = addr.ToString();
            if (addr.AddressFamily == AddressFamily.InterNetwork)
                prefix = "/ip4";
            else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                prefix = "/ip6";
            return string.Join('/', prefix, s, L4Protocol, port);
        }

        /// <summary>
        /// IPAddrFromMultiaddr converts "/dns4/localhost/tcp/8080" to "192.168.0.1:8080".
        /// </summary>
        /// <param name="multiaddr"></param>
        /// <returns></returns>
        public static string IPAddrFromMultiaddr(string multiaddr)
        {
            return FromString(multiaddr).ToIPAddressString();
        }
    }
}
