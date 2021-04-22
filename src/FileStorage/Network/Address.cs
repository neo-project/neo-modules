using Multiformats.Address;
using Multiformats.Address.Net;
using System;
using System.Net.Sockets;

namespace Neo.FileStorage.Network
{
    public class Address : IEquatable<Address>
    {
        public const string L4Protocol = "tcp";

        private Multiaddress ma;

        public string String() => ma.ToString();

        public string IPAddressString()
        {
            return ma.ToEndPoint().ToString();
        }

        public static Address AddressFromString(string s)
        {
            var m = Multiaddress.Decode(s);
            if (m is null)
            {
                var s2 = MultiAddrStringFromHostAddr(s);
                m = Multiaddress.Decode(s2);
            }
            return new Address { ma = m };
        }

        /// <summary>
        /// multiaddrStringFromHostAddr converts "localhost:8080" to "/dns4/localhost/tcp/8080"
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        private static string MultiAddrStringFromHostAddr(string host)
        {
            var endPoint = Multiaddress.Decode(host).ToEndPoint();
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

        bool IEquatable<Address>.Equals(Address other)
        {
            if (other is null) return false;
            return other.ma.Equals(ma);
        }

        /// <summary>
        /// IPAddrFromMultiaddr converts "/dns4/localhost/tcp/8080" to "192.168.0.1:8080".
        /// </summary>
        /// <param name="multiaddr"></param>
        /// <returns></returns>
        public static string IPAddrFromMultiaddr(string multiaddr)
        {
            return AddressFromString(multiaddr).IPAddressString();
        }
    }
}
