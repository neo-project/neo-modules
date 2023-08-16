using Neo.Network.P2P.Payloads;

namespace Neo.Plugins.RestServer.Models
{
    public class WitnessRuleModel
    {
        public WitnessRuleAction Action { get; set; }
        public WitnessConditionModel Condition { get; set; }
    }
}
