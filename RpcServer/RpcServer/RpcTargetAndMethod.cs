using System.Reflection;

namespace Neo.Plugins.RpcServer
{
    internal class RpcTargetAndMethod
    {
        public object Target { get; set; }

        public MethodInfo Method { get; set; }
    }
}
