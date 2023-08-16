using Neo.Network.P2P.Payloads;
using Neo.Network.P2P.Payloads.Conditions;
using Neo.Plugins.RestServer.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Extensions
{
    public static class ModelExtensions
    {
        public static ExecutionEngineModel ToModel(this ApplicationEngine ae) =>
            new()
            {
                GasConsumed = ae.GasConsumed,
                State = ae.State,
                Notifications = ae.Notifications,
                ResultStack = ae.ResultStack,
                FaultException = ae.FaultException,
            };

        public static ContractModel ToModel(this ContractState cs) =>
            new()
            {
                Id = cs.Id,
                Name = cs.Manifest.Name,
                Hash = cs.Hash,
                Manifest = cs.Manifest.ToModel(),
                Nef = cs.Nef.ToModel(),
            };


        public static BlockModel ToModel(this Block block) =>
            new()
            {
                Timestamp = block.Timestamp,
                Version = block.Version,
                PrimaryIndex = block.PrimaryIndex,
                Index = block.Index,
                Nonce = block.Nonce,
                Hash = block.Hash,
                MerkleRoot = block.MerkleRoot,
                PrevHash = block.PrevHash,
                NextConsensus = block.NextConsensus,
                Witness = block.Witness.ToModel(),
                Transactions = block.Transactions?.Length == 0 ? Enumerable.Empty<BlockTransactionModel>() : block.Transactions.Select(s => s.ToModel()),
                Size = block.Size,
            };

        public static BlockHeaderModel ToHeaderModel(this Block block) =>
            new()
            {
                Timestamp = block.Timestamp,
                Version = block.Version,
                PrimaryIndex = block.PrimaryIndex,
                Index = block.Index,
                Nonce = block.Nonce,
                Hash = block.Hash,
                MerkleRoot = block.MerkleRoot,
                PrevHash = block.PrevHash,
                NextConsensus = block.NextConsensus,
                Witness = block.Witness.ToModel(),
                Size = block.Size,
            };

        public static BlockHeaderModel ToModel(this Header block) =>
            new()
            {
                Timestamp = block.Timestamp,
                Version = block.Version,
                PrimaryIndex = block.PrimaryIndex,
                Index = block.Index,
                Nonce = block.Nonce,
                Hash = block.Hash,
                MerkleRoot = block.MerkleRoot,
                PrevHash = block.PrevHash,
                NextConsensus = block.NextConsensus,
                Witness = block.Witness.ToModel(),
                Size = block.Size,
            };

        public static BlockTransactionModel ToModel(this Transaction tx) =>
            new()
            {
                Hash = tx.Hash,
                Sender = tx.Sender,
                Script = tx.Script,
                FeePerByte = tx.FeePerByte,
                NetworkFee = tx.NetworkFee,
                SystemFee = tx.SystemFee,
                Nonce = tx.Nonce,
                Version = tx.Version,
                ValidUntilBlock = tx.ValidUntilBlock,
                Witnesses = tx.Witnesses.Select(s => s.ToModel()),
                Signers = tx.Signers.Select(s => s.ToModel()),
                Attributes = tx.Attributes.Select(s => s.ToModel()),
                Size = tx.Size,
            };

        public static TransactionStateModel ToModel(this TransactionState txst, Block block) =>
            new()
            {
                Block = block.ToHeaderModel(),
                Hash = txst.Transaction.Hash,
                Sender = txst.Transaction.Sender,
                Script = txst.Transaction.Script,
                FeePerByte = txst.Transaction.FeePerByte,
                NetworkFee = txst.Transaction.NetworkFee,
                SystemFee = txst.Transaction.SystemFee,
                Nonce = txst.Transaction.Nonce,
                Version = txst.Transaction.Version,
                ValidUntilBlock = txst.Transaction.ValidUntilBlock,
                Witnesses = txst.Transaction.Witnesses.Select(s => s.ToModel()),
                Signers = txst.Transaction.Signers.Select(s => s.ToModel()),
                Attributes = txst.Transaction.Attributes.Select(s => s.ToModel()),
                Size = txst.Transaction.Size,
                Transfers = Enumerable.Empty<TransactionTransferModel>(),
            };

        public static TransactionAttributeModel ToModel(this TransactionAttribute attr) =>
            new()
            {
                AllowMultiple = attr.AllowMultiple,
                Type = attr.Type,
            };

        public static WitnessModel ToModel(this Witness witness) =>
            new()
            {
                InvocationScript = witness.InvocationScript,
                VerificationScript = witness.VerificationScript,
                ScriptHash = witness.ScriptHash,
            };

        public static SignerModel ToModel(this Signer signer) =>
            new()
            {
                Rules = signer.Rules.Select(s => s.ToModel()),
                Account = signer.Account,
                AllowedContracts = signer.AllowedContracts,
                AllowedGroups = signer.AllowedGroups,
                Scopes = signer.Scopes,
            };

        public static WitnessRuleModel ToModel(this WitnessRule wr) =>
            new()
            {
                Action = wr.Action,
                Condition = wr.Condition.ToModel(),
            };

        public static WitnessConditionModel ToModel(this WitnessCondition wc) =>
            new()
            {
                Type = wc.Type,
            };

        public static NefFileModel ToModel(this NefFile nef) =>
            new()
            {
                CheckSum = nef.CheckSum,
                Compiler = nef.Compiler,
                Script = nef.Script,
                Source = nef.Source,
                Tokens = nef.Tokens.Select(s => s.ToModel()),
            };

        public static MethodTokenModel ToModel(this MethodToken mt) =>
            new()
            {
                Hash = mt.Hash,
                Method = mt.Method,
                ParametersCount = mt.ParametersCount,
                HasReturnValue = mt.HasReturnValue,
                CallFlags = mt.CallFlags,
            };

        public static ContractModel ToModel(this NativeContract nc) =>
            new()
            {
                Id = nc.Id,
                Name = nc.Name,
                Hash = nc.Hash,
                Manifest = nc.Manifest.ToModel(),
            };

        public static ManifestModel ToModel(this ContractManifest cm) =>
            new()
            {
                Name = cm.Name,
                Abi = cm.Abi.ToModel(),
                Groups = cm.Groups.Select(s => s.ToModel()),
                Permissions = cm.Permissions.Select(s => s.ToModel()),
                Trusts = cm.Trusts.Select(s => s.ToModel()),
                SupportedStandards = cm.SupportedStandards,
                Extra = cm.Extra?.Count > 0 ?
                    new JObject(cm.Extra.Properties.Select(s => new JProperty(s.Key.ToString(), s.Value.AsString()))) :
                    null,
            };

        public static ContractAbiModel ToModel(this ContractAbi ca) =>
            new()
            {
                Methods = ca.Methods.Select(m => m.ToModel()),
                Events = ca.Events.Select(s => s.ToModel()),
            };

        public static ContractEventDescriptorModel ToModel(this ContractEventDescriptor ced) =>
            new()
            {
                Name = ced.Name,
                Parameters = ced.Parameters.Select(s => s.ToModel()),
            };

        public static ContractMethodDescriptorModel ToModel(this ContractMethodDescriptor cmd) =>
            new()
            {
                Name = cmd.Name,
                Safe = cmd.Safe,
                Offset = cmd.Offset,
                Parameters = cmd.Parameters.Select(s => s.ToModel()),
                ReturnType = cmd.ReturnType,
            };

        public static ContractParameterDefinitionModel ToModel(this ContractParameterDefinition cpd) =>
            new()
            {
                Name = cpd.Name,
                Type = cpd.Type,
            };

        public static ContractPermissionModel ToModel(this ContractPermission cp) =>
            new()
            {
                Contract = cp.Contract.ToJson().AsString(),//.ToModel(),
                Methods = cp.Methods.Count == 0 ? "*" : cp.Methods.Select(s => s.ToString()).ToArray(),
            };

        public static ContractGroupModel ToModel(this ContractGroup cg) =>
            new()
            {
                PubKey = cg.PubKey,
                Signature = cg.Signature,
            };

        public static ContractPermissionDescriptorModel ToModel(this ContractPermissionDescriptor cpd) =>
            new()
            {
                Group = cpd.Group,
                Hash = cpd.Hash,
                IsGroup = cpd.IsGroup,
                IsHash = cpd.IsHash,
                IsWildcard = cpd.IsWildcard,
            };
    }
}
