namespace Neo.Plugins.RestServer.Models
{
    public class ContractDeploymentModel
    {
        public ContractDeploymentInfoModel[] Senders { get; set; }
        public ContractDeploymentInfoModel[] Witnesses { get; set; }
    }

    public class ContractDeploymentInfoModel
    {
        public UInt256 TransactionHash { get; set; }
        public UInt160 Address { get; set; }
    }
}
