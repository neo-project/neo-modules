using System.Linq;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.WebSocketServer.Filters;
namespace Neo.Plugins.WebSocketServer;

public static class EventUtils
{
    public static bool Matches(IComparator f, IContainer r)
    {
        if (f.EventID != r.EventID) return false;
        switch (f.EventID)
        {
            case EventId.BlockEventId:
                {
                    var blockFilter = (BlockFilter)f.Filter;
                    var block = (Block)r.EventPayload;
                    var primaryOk = blockFilter.Primary == null || blockFilter.Primary == block.PrimaryIndex;
                    var sinceOk = blockFilter.Since == null || blockFilter.Since <= block.Index;
                    var tillOk = blockFilter.Till == null || block.Index <= blockFilter.Till;
                    return primaryOk && sinceOk && tillOk;
                }
            case EventId.TransactionEventId:
                {
                    var txFilter = (TxFilter)f.Filter;
                    var tx = (Transaction)r.EventPayload;
                    var senderOk = txFilter.Sender == null || tx.Sender.Equals(txFilter.Sender);
                    var signerOk = true;
                    if (txFilter.Signer == null) return senderOk;
                    signerOk = tx.Signers.Any(signer => signer.Account.Equals(txFilter.Signer));
                    return senderOk && signerOk;
                }
            case EventId.NotificationEventId:
                {
                    var notificationFilter = (NotificationFilter)f.Filter;
                    var notification = (NotificationEvent)r.EventPayload;
                    var hashOk = notificationFilter.Contract == null || notification.Contract.Equals(notificationFilter.Contract);
                    var nameOk = notificationFilter.Name == null || notification.Name.Equals(notificationFilter.Name);
                    return hashOk && nameOk;
                }
            case EventId.ExecutionEventId:
                {
                    var executionFilter = (ExecutionFilter)f.Filter;
                    var execResult = (ExecutionEvent)r.EventPayload;
                    var stateOk = executionFilter.State == null || execResult.VmState.ToString() == executionFilter.State;
                    var containerOk = executionFilter.Container == null || execResult.Container.Equals(executionFilter.Container);
                    return stateOk && containerOk;
                }
            default:
                return false;
        }

    }

}
