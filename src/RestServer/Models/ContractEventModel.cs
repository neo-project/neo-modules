namespace Neo.Plugins.RestServer.Models
{
    public class ContractEventModel
    {
        public BlockHeaderModel Block { get; set; }
        public BlockTransactionModel Transaction { get; set; }
        public BlockchainEventModel Event { get; set; }
    }
}
