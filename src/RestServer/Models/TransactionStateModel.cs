namespace Neo.Plugins.RestServer.Models
{
    public class TransactionStateModel : BlockTransactionModel
    {
        public BlockHeaderModel Block { get; set; }
        public IEnumerable<TransactionTransferModel> Transfers { get; set; }
    }
}
