using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class AuditSettlementDeps : SettlementDeps
    {
        public override void Transfer(OwnerID sender, OwnerID recipient, long amount, byte[] details)
        {
            transfer(sender, recipient, amount, Utility.StrictUTF8.GetBytes("settlement-audit"));
        }
    }
}
