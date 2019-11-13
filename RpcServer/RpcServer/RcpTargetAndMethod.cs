using System.Reflection;

namespace Neo.Plugins.RpcServer
{
    internal class RcpTargetAndMethod
    {
        public object Target { get; set; }

        public MethodInfo Method { get; set; }
    }
}
