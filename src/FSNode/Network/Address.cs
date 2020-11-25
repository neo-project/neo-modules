using Multiformats.Address;
using Multiformats.Address.Net;
using System.Net.Sockets;

namespace Neo.Fs.Network
{
    public class Address
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

        // multiaddrStringFromHostAddr converts "localhost:8080" to "/dns4/localhost/tcp/8080"
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

        public bool IsLocalAddress(ILocalAddressSource src)
        {
            return src.LocalAddress().ma.Equals(this.ma);
        }
    }

    // ILocalAddressSource is an interface of local
    // network address container with read access.
    public interface ILocalAddressSource
    {
        Address LocalAddress();
    }
}
