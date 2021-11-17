using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Multiformats.Address;
using Multiformats.Address.Protocols;

namespace Neo.FileStorage.Network
{
    public class Address : IEquatable<Address>
    {
        public const string L4Protocol = "tcp";
        public static readonly Dictionary<string, string> SubstituteProtocols = new() { { "tls", "https" } };
        private readonly Multiaddress ma;

        public Address() { }

        public Address(Multiaddress m)
        {
            RpcSupportedCheck(m);
            ma = m;
        }

        public string ToHostAddressString()
        {
            return DialArgs(ma);
        }

        public string ToRpcAddressString()
        {
            string host = ToHostAddressString();
            if (ma.Protocols.Exists(p => p is HTTPS))
                return "https://" + host;
            else
                return "http://" + host;
        }

        public static Address FromString(string s)
        {
            Multiaddress m;
            try
            {
                m = Multiaddress.Decode(s);
            }
            catch (NotSupportedException nse)
            {
                if (SubstituteProtocols.TryGetValue(nse.Message, out string sub))
                {
                    m = Multiaddress.Decode(s.Replace(nse.Message, sub));
                    return new Address(m);
                }
                else
                {
                    m = Multiaddress.Decode(MultiAddrStringFromHostAddr(s));
                }
            }
            return new Address(m);
        }

        private static void RpcSupportedCheck(Multiaddress ma)
        {
            if (!ma.Protocols.Any(p => p is HTTPS || p is TCP || p is HTTP))
                throw new NotSupportedException("no rpc supported protocols");
        }

        public bool Equals(Address other)
        {
            if (other is null) return false;
            return other.ma.Equals(ma);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj is Address addr)
                return Equals(addr);
            return false;
        }

        public override int GetHashCode() => ma.ToString().GetHashCode();

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

        public override string ToString()
        {
            string str = ma.ToString();
            foreach (var (inuse, deprecated) in SubstituteProtocols)
                str = str.Replace(deprecated, inuse);
            return str;
        }

        private static string DialArgs(Multiaddress ma)
        {
            var res = DialArgsComponents(ma);
            if (res.HostName)
            {
                return res.Network switch
                {
                    "ip" or "ip4" or "ip6" => res.IP,
                    "tcp" or "tcp4" or "tcp6" or "udp" or "udp4" or "udp6" => res.IP + ":" + res.Port,
                    _ => throw new InvalidOperationException("unreachable"),
                };
            }
            return res.Network switch
            {
                "ip4" => res.IP,
                "tcp4" or "udp4" => res.IP + ":" + res.Port,
                "tcp6" or "udp6" => "[" + res.IP + "]" + ":" + res.Port,
                "unix" => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? res.IP.Replace("/", "\\") : res.IP,
                _ => throw new InvalidOperationException($"{ma} is not a 'thin waist' address"),
            };
        }

        private class DialArgsComponentsResult
        {
            public string Network;
            public string IP;
            public string Port;
            public bool HostName;
        }

        private static DialArgsComponentsResult DialArgsComponents(Multiaddress ma)
        {
            DialArgsComponentsResult res = new();
            foreach (var p in ma.Protocols)
            {
                switch (res.Network)
                {
                    case null:
                        {
                            switch (p)
                            {
                                case IP4 ip4:
                                    {
                                        res.Network = "ip4";
                                        res.IP = ip4.Value.ToString();
                                        continue;
                                    }
                                case IP6 ip6:
                                    {
                                        res.Network = "ip6";
                                        res.IP = ip6.Value.ToString();
                                        continue;
                                    }
                                case DNS dns:
                                    {
                                        res.Network = "ip";
                                        res.IP = dns.Value.ToString();
                                        res.HostName = true;
                                        continue;
                                    }
                                case DNS4 dns4:
                                    {
                                        res.Network = "ip4";
                                        res.IP = dns4.Value.ToString();
                                        res.HostName = true;
                                        continue;
                                    }
                                case DNS6 dns6:
                                    {
                                        res.Network = "ip6";
                                        res.IP = dns6.Value.ToString();
                                        res.HostName = true;
                                        continue;
                                    }
                                case Unix unix:
                                    {
                                        res.Network = "unix";
                                        res.IP = unix.Value.ToString();
                                        return res;
                                    }
                            }
                            break;
                        }
                    case "ip":
                        {
                            switch (p)
                            {
                                case UDP: res.Network = "udp"; break;
                                case TCP: res.Network = "tcp"; break;
                                default: return res;
                            }
                            res.Port = p.Value.ToString();
                            break;
                        }
                    case "ip4":
                        {
                            switch (p)
                            {
                                case UDP: res.Network = "udp4"; break;
                                case TCP: res.Network = "tcp4"; break;
                                default: return res;
                            }
                            res.Port = p.Value.ToString();
                            break;
                        }
                    case "ip6":
                        {
                            switch (p)
                            {
                                case UDP: res.Network = "udp6"; break;
                                case TCP: res.Network = "tcp6"; break;
                                default: return res;
                            }
                            res.Port = p.Value.ToString();
                            break;
                        }
                }
            }
            return res;
        }
    }
}
