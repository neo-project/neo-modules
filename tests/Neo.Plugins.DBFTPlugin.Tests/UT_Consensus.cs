using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Consensus;
using Neo.Cryptography;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using Neo.Plugins;
using static Neo.SmartContract.Native.NeoToken;
using ECPoint = Neo.Cryptography.ECC.ECPoint;

namespace Neo.Consensus;

[TestClass]
public class ConsensusTests : TestKit
{
    private KeyPair[] _kpArray;

    [TestInitialize]
    public void TestSetup()
    {
        TestBlockchain.InitializeMockNeoSystem();

        var moked = new[] {
            ECPoint.Parse("02486fd15702c4490a26703112a5cc1d0923fd697a33406bd5a1c00e0013b09a70", Neo.Cryptography.ECC.ECCurve.Secp256r1),
            ECPoint.Parse("024c7b7fb6c310fccf1ba33b082519d82964ea93868d676662d4a59ad548df0e7d", Neo.Cryptography.ECC.ECCurve.Secp256r1),
            ECPoint.Parse("02aaec38470f6aad0042c6e877cfd8087d2676b0f516fddd362801b9bd3936399e", Neo.Cryptography.ECC.ECCurve.Secp256r1),
            ECPoint.Parse("02ca0e27697b9c248f6f16e085fd0061e26f44da85b58ee835c110caa5ec3ba554", Neo.Cryptography.ECC.ECCurve.Secp256r1),
            ECPoint.Parse("02df48f60e8f3e01c48ff40b9b7f1310d7a8b2a193188befe1c2e3df740e895093", Neo.Cryptography.ECC.ECCurve.Secp256r1),
            ECPoint.Parse("03b209fd4f53a7170ea4444e0cb0a6bb6a53c2bd016926989cf85f9b0fba17a70c", Neo.Cryptography.ECC.ECCurve.Secp256r1),
            ECPoint.Parse("03b8d9d5771d8f513aa0869b9cc8d50986403b78c6da36890638c3d46a5adce04a", Neo.Cryptography.ECC.ECCurve.Secp256r1)
        };

        _kpArray = new KeyPair[7]
        {
            UT_Crypto.generateKey(32), // not used, kept for index consistency, didactically
            UT_Crypto.generateKey(32),
            UT_Crypto.generateKey(32),
            UT_Crypto.generateKey(32),
            UT_Crypto.generateKey(32),
            UT_Crypto.generateKey(32),
            UT_Crypto.generateKey(32)
        }.OrderBy(p => p.PublicKey).ToArray();

        // TestBlockchain.AddWhiteList(TestBlockchain.DefaultExtensibleWitnessWhiteList
        //     .Concat(moked.Select(u => Contract.CreateSignatureContract(u).ScriptHash))
        //     .Concat(_kpArray.Select(u => Contract.CreateSignatureContract(u.PublicKey).ScriptHash))
        //     .ToArray());
    }

    [TestCleanup]
    public void Cleanup()
    {
        // TestBlockchain.AddWhiteList(TestBlockchain.DefaultExtensibleWitnessWhiteList);
        Shutdown();
    }

    [TestMethod]
    public void ConsensusService_SingleNodeActors_OnStart_PrepReq_PrepResponses_Commits()
    {
        var mockNeoSystem = new Mock<TestNeoSystem>();
        var mockWallet = new Mock<Wallet>();
        mockWallet.Setup(p => p.GetAccount(It.IsAny<UInt160>())).Returns<UInt160>(p => new TestWalletAccount(p));
        Console.WriteLine($"\n(UT-Consensus) Wallet is: {mockWallet.Object.GetAccount(UInt160.Zero).GetKey().PublicKey}");
        var mockContext = new Mock<ConsensusContext>(mockNeoSystem.Object, mockWallet.Object, ProtocolSettings.Default);
        var timeValues = new[] {
            new DateTime(1980, 06, 01, 0, 0, 1, 001, DateTimeKind.Utc),  // For tests, used below
            new DateTime(1980, 06, 01, 0, 0, 3, 001, DateTimeKind.Utc),  // For receiving block
            new DateTime(1980, 05, 01, 0, 0, 5, 001, DateTimeKind.Utc),  // For Initialize
            new DateTime(1980, 06, 01, 0, 0, 15, 001, DateTimeKind.Utc), // unused
        };
        for (var i = 0; i < timeValues.Length; i++)
            Console.WriteLine($"time {i}: {timeValues[i]} ");

        // GMT: Sunday, June 1, 1980 12:00:01.001 AM
        // check basic ConsensusContext
        // mockConsensusContext.Object.block_received_time.ToTimestamp().Should().Be(4244941697); //1968-06-01 00:00:01
        // ============================================================================
        //                      creating ConsensusService actor
        // ============================================================================
        const ulong defaultTimestamp = 328665601001;

        int timeIndex = 0;
        var timeMock = new Mock<TestTimeProvider>();
        timeMock.SetupGet(tp => tp.UtcNow).Returns(() => timeValues[timeIndex]);
        //.Callback(() => timeIndex = timeIndex + 1); //Comment while index is not fixed

        TestTimeProvider.Current = timeMock.Object;
        TestTimeProvider.Current.UtcNow.ToTimestampMS().Should().Be(defaultTimestamp); //1980-06-01 00:00:15:001

        // Creating a test block
        Header header = new Header();
        TestUtils.SetupHeaderWithValues(
            header,
            UInt256.Zero,
            out UInt256 merkRootVal,
            out UInt160 val160,
            out ulong timestampVal,
            out ulong nonceVal,
            out uint indexVal,
            out Witness scriptVal);
        header.Size.Should().Be(105);
        Console.WriteLine($"header {header} hash {header.Hash} {header.PrevHash} timestamp {timestampVal}");
        timestampVal.Should().Be(defaultTimestamp);
        TestProbe subscriber = CreateTestProbe();
        TestActorRef<ConsensusService> actorConsensus = ActorOfAsTestActorRef<ConsensusService>(
            Akka.Actor.Props.Create(() => (ConsensusService)Activator.CreateInstance(typeof(ConsensusService), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { subscriber, subscriber, subscriber, mockContext.Object }, null))
        );

        var testPersistCompleted = new Blockchain.PersistCompleted
        {
            Block = new Block
            {
                Header = new Header
                {
                    PrevHash = header.PrevHash,
                    MerkleRoot = header.MerkleRoot,
                    Timestamp = header.Timestamp,
                    Nonce = header.Nonce,
                    PrimaryIndex = header.PrimaryIndex,
                    Witness = header.Witness,
                    Index = header.Index,
                    NextConsensus = header.NextConsensus
                },
                Transactions = Array.Empty<Transaction>()
            }
        };
        Console.WriteLine("\n==========================");
        Console.WriteLine("Telling a new block to actor consensus...");
        Console.WriteLine("will trigger OnPersistCompleted without OnStart flag!");
        // OnPersist will not launch timer, we need OnStart
        actorConsensus.Tell(testPersistCompleted);
        Console.WriteLine("\n==========================");

        Console.WriteLine("\n==========================");
        Console.WriteLine("will start consensus!");
        actorConsensus.Tell(new ConsensusService.Start { });

        Console.WriteLine("Waiting for subscriber recovery message...");
        // The next line force a waits, then, subscriber keeps running its thread
        // In the next case it waits for a Msg of type LocalNode.SendDirectly
        // As we may expect, as soon as consensus start it sends a RecoveryRequest of this aforementioned type
        var askingForInitialRecovery = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        Console.WriteLine($"Recovery Message I: {askingForInitialRecovery}");
        foreach (var validator in mockContext.Object.Validators)
        {
            mockContext.Object.LastSeenMessage[validator] = 0;
        }
        // Ensuring cast of type ConsensusPayload from the received message from subscriber
        ExtensiblePayload initialRecoveryPayload = (ExtensiblePayload)askingForInitialRecovery.Inventory;
        // Ensuring casting of type RecoveryRequest
        RecoveryRequest rrm = initialRecoveryPayload.Data.AsSerializable<RecoveryRequest>();
        rrm.Timestamp.Should().Be(defaultTimestamp);

        Console.WriteLine("Waiting for backup ChangeView... ");
        var backupOnAskingChangeView = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        var changeViewPayload = (ExtensiblePayload)backupOnAskingChangeView.Inventory;
        ChangeView cvm = changeViewPayload.Data.AsSerializable<ChangeView>();
        cvm.Timestamp.Should().Be(defaultTimestamp);
        cvm.ViewNumber.Should().Be(0);
        cvm.Reason.Should().Be(ChangeViewReason.Timeout);

        // Original Contract
        Contract originalContract = Contract.CreateMultiSigContract(mockContext.Object.M, mockContext.Object.Validators);
        Console.WriteLine($"\nORIGINAL Contract is: {originalContract.ScriptHash}");
        Console.WriteLine($"ORIGINAL NextConsensus: {mockContext.Object.Block.NextConsensus}\nENSURING values...");
        originalContract.ScriptHash.Should().Be(UInt160.Parse("0xe239c7228fa6b46cc0cf43623b2f934301d0b4f7"));
        mockContext.Object.Block.NextConsensus.Should().Be(UInt160.Parse("0xe239c7228fa6b46cc0cf43623b2f934301d0b4f7"));

        Console.WriteLine("\n==========================");
        Console.WriteLine("will trigger OnPersistCompleted again with OnStart flag!");
        actorConsensus.Tell(testPersistCompleted);
        Console.WriteLine("\n==========================");

        // Disabling flag ViewChanging by reverting cache of changeview that was sent
        mockContext.Object.ChangeViewPayloads[mockContext.Object.MyIndex] = null;
        Console.WriteLine("Forcing Failed nodes for recovery request... ");
        mockContext.Object.CountFailed.Should().Be(0);
        mockContext.Object.LastSeenMessage.Clear();
        mockContext.Object.CountFailed.Should().Be(7);
        Console.WriteLine("\nWaiting for recovery due to failed nodes... ");
        var backupOnRecoveryDueToFailedNodes = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        var recoveryPayload = (ExtensiblePayload)backupOnRecoveryDueToFailedNodes.Inventory;
        rrm = recoveryPayload.Data.AsSerializable<RecoveryRequest>();
        rrm.Timestamp.Should().Be(defaultTimestamp);

        Console.WriteLine("will create template MakePrepareRequest...");
        mockContext.Object.PrevHeader.Timestamp = defaultTimestamp;
        mockContext.Object.PrevHeader.NextConsensus.Should().Be(UInt160.Parse("0xe239c7228fa6b46cc0cf43623b2f934301d0b4f7"));
        var prepReq = mockContext.Object.MakePrepareRequest();
        var ppToSend = prepReq.Data.AsSerializable<PrepareRequest>();
        // Forcing hashes to 0 because mempool is currently shared
        ppToSend.TransactionHashes = new UInt256[0];
        ppToSend.TransactionHashes.Length.Should().Be(0);
        Console.WriteLine($"\nAsserting PreparationPayloads is 1 (After MakePrepareRequest)...");
        mockContext.Object.PreparationPayloads.Count(p => p != null).Should().Be(1);
        mockContext.Object.PreparationPayloads[ppToSend.ValidatorIndex] = null;

        Console.WriteLine("will tell prepare request!");
        prepReq = new ExtensiblePayload
        {
            Category = "Consensus",
            ValidBlockStart = 0,
            ValidBlockEnd = ppToSend.BlockIndex,
            Sender = prepReq.Sender,
            Data = ppToSend.ToArray(),
            Witness = prepReq.Witness
        };
        TellConsensusPayload(actorConsensus, prepReq);
        Console.WriteLine("Waiting for something related to the PrepRequest...\nNothing happens...Recovery will come due to failed nodes");
        var backupOnRecoveryDueToFailedNodesII = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        var recoveryPayloadII = (ExtensiblePayload)backupOnRecoveryDueToFailedNodesII.Inventory;
        rrm = recoveryPayloadII.Data.AsSerializable<RecoveryRequest>();
        Console.WriteLine($"\nAsserting PreparationPayloads is 0...");
        mockContext.Object.PreparationPayloads.Count(p => p != null).Should().Be(0);
        Console.WriteLine($"\nAsserting CountFailed is 6...");
        mockContext.Object.CountFailed.Should().Be(6);

        Console.WriteLine("\nFailed because it is not primary and it created the prereq...Time to adjust");
        ppToSend.ValidatorIndex = 1; //simulating primary as prepreq creator (signature is skip, no problem)
        // cleaning old try with Self ValidatorIndex
        mockContext.Object.PreparationPayloads[mockContext.Object.MyIndex] = null;

        prepReq = new ExtensiblePayload
        {
            Category = "Consensus",
            ValidBlockStart = 0,
            ValidBlockEnd = ppToSend.BlockIndex,
            Sender = mockContext.Object.GetSender(ppToSend.ValidatorIndex),
            Data = ppToSend.ToArray(),
            Witness = prepReq.Witness
        };
        TellConsensusPayload(actorConsensus, prepReq);
        var OnPrepResponse = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        var prepResponsePayload = (ExtensiblePayload)OnPrepResponse.Inventory;
        PrepareResponse prm = prepResponsePayload.Data.AsSerializable<PrepareResponse>();
        prm.PreparationHash.Should().Be(prepReq.Hash);
        Console.WriteLine("\nAsserting PreparationPayloads count is 2...");
        mockContext.Object.PreparationPayloads.Count(p => p != null).Should().Be(2);
        Console.WriteLine($"\nAsserting CountFailed is 5...");
        mockContext.Object.CountFailed.Should().Be(5);

        // Simulating CN 3
        TellConsensusPayload(actorConsensus, GetPayloadAndModifyValidator(mockContext.Object, prepResponsePayload, 2));
        //Waiting for RecoveryRequest for a more deterministic UT
        backupOnRecoveryDueToFailedNodes = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        recoveryPayload = (ExtensiblePayload)backupOnRecoveryDueToFailedNodes.Inventory;
        rrm = recoveryPayload.Data.AsSerializable<RecoveryRequest>();
        rrm.Timestamp.Should().Be(defaultTimestamp);
        //Asserts
        Console.WriteLine("\nAsserting PreparationPayloads count is 3...");
        mockContext.Object.PreparationPayloads.Count(p => p != null).Should().Be(3);
        Console.WriteLine($"\nAsserting CountFailed is 4...");
        mockContext.Object.CountFailed.Should().Be(4);

        // Simulating CN 5
        TellConsensusPayload(actorConsensus, GetPayloadAndModifyValidator(mockContext.Object, prepResponsePayload, 4));
        //Waiting for RecoveryRequest for a more deterministic UT
        backupOnRecoveryDueToFailedNodes = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        recoveryPayload = (ExtensiblePayload)backupOnRecoveryDueToFailedNodes.Inventory;
        rrm = recoveryPayload.Data.AsSerializable<RecoveryRequest>();
        rrm.Timestamp.Should().Be(defaultTimestamp);
        //Asserts
        Console.WriteLine("\nAsserting PreparationPayloads count is 4...");
        mockContext.Object.PreparationPayloads.Count(p => p != null).Should().Be(4);
        Console.WriteLine($"\nAsserting CountFailed is 3...");
        mockContext.Object.CountFailed.Should().Be(3);

        // Simulating CN 4
        TellConsensusPayload(actorConsensus, GetPayloadAndModifyValidator(mockContext.Object, prepResponsePayload, 3));
        var onCommitPayload = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        var commitPayload = (ExtensiblePayload)onCommitPayload.Inventory;
        Commit cm = commitPayload.Data.AsSerializable<Commit>();
        Console.WriteLine("\nAsserting PreparationPayloads count is 5...");
        mockContext.Object.PreparationPayloads.Count(p => p != null).Should().Be(5);
        Console.WriteLine("\nAsserting CountCommitted is 1...");
        mockContext.Object.CountCommitted.Should().Be(1);
        Console.WriteLine($"\nAsserting CountFailed is 2...");
        mockContext.Object.CountFailed.Should().Be(2);

        Console.WriteLine($"ORIGINAL BlockHash: {mockContext.Object.Block.Hash}");
        Console.WriteLine($"ORIGINAL Block NextConsensus: {mockContext.Object.Block.NextConsensus}");

        foreach (var t in mockContext.Object.Validators)
            Console.WriteLine($"{t}/{Contract.CreateSignatureContract(t).ScriptHash}");
        mockContext.Object.Validators = new ECPoint[7]
        {
            _kpArray[0].PublicKey,
            _kpArray[1].PublicKey,
            _kpArray[2].PublicKey,
            _kpArray[3].PublicKey,
            _kpArray[4].PublicKey,
            _kpArray[5].PublicKey,
            _kpArray[6].PublicKey
        };
        Console.WriteLine($"Generated keypairs PKey:");
        //refresh LastSeenMessage
        mockContext.Object.LastSeenMessage.Clear();
        foreach (var t in mockContext.Object.Validators)
            Console.WriteLine($"{t}/{Contract.CreateSignatureContract(t).ScriptHash}");
        var updatedContract = Contract.CreateMultiSigContract(mockContext.Object.M, mockContext.Object.Validators);
        Console.WriteLine($"\nContract updated: {updatedContract.ScriptHash}");

        // ===============================================================
        TestCachedCommittee cachedCommittee = new TestCachedCommittee(mockContext.Object.Validators.Select(p => (p, BigInteger.Zero)));
        mockContext.Object.Snapshot.Delete(CreateStorageKeyForNativeNeo(14));
        mockContext.Object.Snapshot.Add(CreateStorageKeyForNativeNeo(14), new StorageItem()
        {
            Value = BinarySerializer.Serialize(cachedCommittee.ToStackItem(null), 4096)
        });
        mockContext.Object.Snapshot.Commit();
        // ===============================================================

        // Forcing next consensus
        var originalBlockHashData = mockContext.Object.Block.Hash;
        mockContext.Object.Block.Header.NextConsensus = updatedContract.ScriptHash;
        mockContext.Object.Block.Header.NextConsensus = updatedContract.ScriptHash;
        var originalBlockMerkleRoot = mockContext.Object.Block.MerkleRoot;
        Console.WriteLine($"\noriginalBlockMerkleRoot: {originalBlockMerkleRoot}");
        var updatedBlockHashData = mockContext.Object.Block.Hash;
        Console.WriteLine($"originalBlockHashData: {originalBlockHashData}");
        Console.WriteLine($"updatedBlockHashData: {updatedBlockHashData}");

        Console.WriteLine("\n\n==========================");
        Console.WriteLine("\nBasic commits Signatures verification");
        // Basic tests for understanding signatures and ensuring signatures of commits are correct on tests


        var cmPayloadTemp = GetCommitPayloadModifiedAndSignedCopy(mockContext.Object, commitPayload, 6, _kpArray[6], updatedBlockHashData.ToArray());
        Crypto.VerifySignature(originalBlockHashData.ToArray(), cm.Signature.Span, mockContext.Object.Validators[0]).Should().BeFalse();
        Crypto.VerifySignature(updatedBlockHashData.ToArray(), cm.Signature.Span, mockContext.Object.Validators[0]).Should().BeFalse();
        Crypto.VerifySignature(originalBlockHashData.ToArray(), cmPayloadTemp.Data.AsSerializable<Commit>().Signature.Span, mockContext.Object.Validators[6]).Should().BeFalse();
        Crypto.VerifySignature(updatedBlockHashData.ToArray(), cmPayloadTemp.Data.AsSerializable<Commit>().Signature.Span, mockContext.Object.Validators[6]).Should().BeTrue();
        Console.WriteLine("\n==========================");

        Console.WriteLine("\n==========================");
        Console.WriteLine("\nCN7 simulation time");
        TellConsensusPayload(actorConsensus, cmPayloadTemp);
        var tempPayloadToBlockAndWait = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        var rmPayload = (ExtensiblePayload)tempPayloadToBlockAndWait.Inventory;
        RecoveryMessage rmm = rmPayload.Data.AsSerializable<RecoveryMessage>();
        Console.WriteLine("\nAsserting CountCommitted is 2...");
        mockContext.Object.CountCommitted.Should().Be(2);
        Console.WriteLine($"\nAsserting CountFailed is 1...");
        mockContext.Object.CountFailed.Should().Be(6);

        Console.WriteLine("\nCN6 simulation time");
        TellConsensusPayload(actorConsensus, GetCommitPayloadModifiedAndSignedCopy(mockContext.Object, commitPayload, 5, _kpArray[5], updatedBlockHashData.ToArray()));
        tempPayloadToBlockAndWait = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        rmPayload = (ExtensiblePayload)tempPayloadToBlockAndWait.Inventory;
        rmm = rmPayload.Data.AsSerializable<RecoveryMessage>();
        Console.WriteLine("\nAsserting CountCommitted is 3...");
        mockContext.Object.CountCommitted.Should().Be(3);
        Console.WriteLine($"\nAsserting CountFailed is 0...");
        mockContext.Object.CountFailed.Should().Be(5);

        Console.WriteLine("\nCN5 simulation time");
        TellConsensusPayload(actorConsensus, GetCommitPayloadModifiedAndSignedCopy(mockContext.Object, commitPayload, 4, _kpArray[4], updatedBlockHashData.ToArray()));
        tempPayloadToBlockAndWait = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        Console.WriteLine("\nAsserting CountCommitted is 4...");
        mockContext.Object.CountCommitted.Should().Be(4);

        // =============================================
        // Testing commit with wrong signature not valid
        // It will be invalid signature because we did not change ECPoint
        Console.WriteLine("\nCN4 simulation time. Wrong signature, KeyPair is not known");
        TellConsensusPayload(actorConsensus, GetPayloadAndModifyValidator(mockContext.Object, commitPayload, 3));
        Console.WriteLine("\nWaiting for recovery due to failed nodes... ");
        var backupOnRecoveryMessageAfterCommit = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        rmPayload = (ExtensiblePayload)backupOnRecoveryMessageAfterCommit.Inventory;
        rmm = rmPayload.Data.AsSerializable<RecoveryMessage>();
        Console.WriteLine("\nAsserting CountCommitted is 4 (Again)...");
        mockContext.Object.CountCommitted.Should().Be(4);
        Console.WriteLine("\nAsserting recovery message Preparation is 5...");
        rmm.PreparationMessages.Count.Should().Be(5);
        Console.WriteLine("\nAsserting recovery message CommitMessages is 4...");
        rmm.CommitMessages.Count().Should().Be(4);
        // =============================================

        Console.WriteLine($"\nForcing block {mockContext.Object.Block.GetHashCode().ToString()} PrevHash to UInt256.Zero");
        // Another option would be to manipulate Blockchain.Singleton.GetSnapshot().Blocks.GetAndChange
        // We would need to get the PrevHash and change the NextConsensus field
        var oldPrevHash = mockContext.Object.Block.PrevHash;
        mockContext.Object.Block.Header.PrevHash = UInt256.Zero;
        //Payload should also be forced, otherwise OnConsensus will not pass
        Console.WriteLine($"\nNew Hash is {mockContext.Object.Block.GetHashCode().ToString()}");
        Console.WriteLine($"\nForcing block VerificationScript to {updatedContract.Script.ToScriptHash()}");
        // The default behavior for BlockBase, when PrevHash = UInt256.Zero, is to use its own Witness
        mockContext.Object.Block.Header.Witness = new Witness { };
        mockContext.Object.Block.Witness.VerificationScript = updatedContract.Script;
        Console.WriteLine($"\nUpdating BlockBase Witness scripthash to: {mockContext.Object.Block.Witness.ScriptHash}");
        Console.WriteLine($"\nNew Hash is {mockContext.Object.Block.GetHashCode().ToString()}");

        Console.WriteLine("\nCN4 simulation time - Final needed signatures");
        TellConsensusPayload(actorConsensus, GetCommitPayloadModifiedAndSignedCopy(mockContext.Object, commitPayload, 3, _kpArray[3], mockContext.Object.Block.Hash.ToArray()));

        Console.WriteLine("\nWait for subscriber Block");
        var utBlock = subscriber.ExpectMsg<Block>();
        Console.WriteLine("\nAsserting CountCommitted is 5...");
        mockContext.Object.CountCommitted.Should().Be(5);

        Console.WriteLine($"\nAsserting block NextConsensus..{utBlock.NextConsensus}");
        utBlock.NextConsensus.Should().Be(updatedContract.ScriptHash);
        Console.WriteLine("\n==========================");

        // =============================================
        Console.WriteLine("\nRecovery simulation...");
        mockContext.Object.CommitPayloads = new ExtensiblePayload[mockContext.Object.Validators.Length];
        // avoiding the BlockSent flag
        mockContext.Object.Block.Transactions = null;
        // ensuring same hash as snapshot
        mockContext.Object.Block.Header.PrevHash = oldPrevHash;

        Console.WriteLine("\nAsserting CountCommitted is 0...");
        mockContext.Object.CountCommitted.Should().Be(0);
        Console.WriteLine($"\nAsserting CountFailed is 0...");
        mockContext.Object.CountFailed.Should().Be(3);
        Console.WriteLine($"\nModifying CountFailed and asserting 7...");
        // This will ensure a non-deterministic behavior after last recovery
        mockContext.Object.LastSeenMessage.Clear();
        mockContext.Object.CountFailed.Should().Be(7);

        TellConsensusPayload(actorConsensus, rmPayload);

        Console.WriteLine("\nWaiting for RecoveryRequest before final asserts...");
        var onRecoveryRequestAfterRecovery = subscriber.ExpectMsg<LocalNode.SendDirectly>();
        var rrPayload = (ExtensiblePayload)onRecoveryRequestAfterRecovery.Inventory;
        var rrMessage = rrPayload.Data.AsSerializable<RecoveryRequest>();

        // It should be 3 because the commit generated by the default wallet is still invalid
        Console.WriteLine("\nAsserting CountCommitted is 3 (after recovery)...");
        mockContext.Object.CountCommitted.Should().Be(3);
        // =============================================

        // =============================================
        // ============================================================================
        //                      finalize ConsensusService actor
        // ============================================================================
        Console.WriteLine("Returning states.");
        // Updating context.Snapshot with the one that was committed
        Console.WriteLine("mockContext Reset for returning Blockchain.Singleton snapshot to original state.");
        mockContext.Object.Reset(0);
        mockContext.Object.Snapshot.Delete(CreateStorageKeyForNativeNeo(14));
        cachedCommittee = new TestCachedCommittee(ProtocolSettings.Default.StandbyCommittee.Select(p => (p, BigInteger.Zero)));
        mockContext.Object.Snapshot.Add(CreateStorageKeyForNativeNeo(14), new StorageItem
        {
            Value = BinarySerializer.Serialize(cachedCommittee.ToStackItem(null), 4096)
        });
        mockContext.Object.Snapshot.Commit();

        Console.WriteLine("mockContext Reset.");
        mockContext.Object.Reset(0);
        Console.WriteLine("TimeProvider Reset.");

        // FIXME: This is not working
        TestTimeProvider.ResetToDefault();

        Console.WriteLine("Finalizing consensus service actor.");
        Sys.Stop(actorConsensus);
        Console.WriteLine("Actor actorConsensus Stopped.\n");
    }

    /// <summary>
    /// Get a clone of a ConsensusPayload that contains a Commit Message, change its currentValidatorIndex and sign it
    /// </summary>
    /// <param name="cpToCopy">ConsensusPayload that will be modified
    /// <param name="vI">new ValidatorIndex for the cpToCopy
    /// <param name="kp">KeyPair that will be used for signing the Commit message used for creating blocks
    /// <param name="blockHashToSign">HashCode of the Block that is being produced and current being signed
    public ExtensiblePayload GetCommitPayloadModifiedAndSignedCopy(ConsensusContext context, ExtensiblePayload cpToCopy, byte vI, KeyPair kp, byte[] blockHashToSign)
    {
        var cpCommitTemp = cpToCopy.ToArray().AsSerializable<ExtensiblePayload>();
        cpCommitTemp.Sender = context.GetSender(vI);
        var oldMessage = cpCommitTemp.Data.AsSerializable<Commit>();
        cpCommitTemp.Data = new Commit
        {
            BlockIndex = oldMessage.BlockIndex,
            ValidatorIndex = vI,
            ViewNumber = oldMessage.ViewNumber,
            Signature = Crypto.Sign(blockHashToSign, kp.PrivateKey, kp.PublicKey.EncodePoint(false).Skip(1).ToArray())
        }.ToArray();
        SignPayload(cpCommitTemp, kp);
        return cpCommitTemp;
    }

    /// <summary>
    /// Get a clone of a ConsensusPayload and change its currentValidatorIndex
    /// </summary>
    /// <param name="cpToCopy">ConsensusPayload that will be modified
    /// <param name="vI">new ValidatorIndex for the cpToCopy
    public ExtensiblePayload GetPayloadAndModifyValidator(ConsensusContext context, ExtensiblePayload cpToCopy, byte vI)
    {
        var cpTemp = cpToCopy.ToArray().AsSerializable<ExtensiblePayload>();
        var message = ConsensusMessage.DeserializeFrom(cpTemp.Data);
        message.ValidatorIndex = vI;
        cpTemp.Data = message.ToArray();
        cpTemp.Sender = context.GetSender(vI);
        return cpTemp;
    }

    private void SignPayload(ExtensiblePayload payload, KeyPair kp)
    {
        var mockNeoSystem = new Mock<TestNeoSystem>();
        var mockSetting = new Mock<TestSetting>();
        ContractParametersContext sc;
        try
        {
            sc = new ContractParametersContext(mockNeoSystem.Object.GetSnapshot(), payload, mockSetting.Object.Network);
            byte[] signature = sc.Verifiable.Sign(kp, mockSetting.Object.Network);
            sc.AddSignature(Contract.CreateSignatureContract(kp.PublicKey), kp.PublicKey, signature);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        payload.Witness = sc.GetWitnesses()[0];
    }

    [TestMethod]
    public void TestSerializeAndDeserializeConsensusContext()
    {
        var mockNeoSystem = new Mock<TestNeoSystem>();
        var mockSetting = new Mock<TestSetting>();
        var mockWallet = new Mock<Wallet>();
        var consensusContext = new ConsensusContext(mockNeoSystem.Object, mockSetting.Object, mockWallet.Object)
        {
            Block = new Block
            {
                Header = new Header
                {
                    PrevHash = mockNeoSystem.Object.GenesisBlock.Hash,
                    Index = 1,
                    Timestamp = 4244941711,
                    NextConsensus = UInt160.Parse("5555AAAA5555AAAA5555AAAA5555AAAA5555AAAA"),
                    PrimaryIndex = 6
                }
            },
            ViewNumber = 2,
            Validators = new ECPoint[7]
            {
                ECPoint.Parse("02486fd15702c4490a26703112a5cc1d0923fd697a33406bd5a1c00e0013b09a70", Neo.Cryptography.ECC.ECCurve.Secp256r1),
                ECPoint.Parse("024c7b7fb6c310fccf1ba33b082519d82964ea93868d676662d4a59ad548df0e7d", Neo.Cryptography.ECC.ECCurve.Secp256r1),
                ECPoint.Parse("02aaec38470f6aad0042c6e877cfd8087d2676b0f516fddd362801b9bd3936399e", Neo.Cryptography.ECC.ECCurve.Secp256r1),
                ECPoint.Parse("02ca0e27697b9c248f6f16e085fd0061e26f44da85b58ee835c110caa5ec3ba554", Neo.Cryptography.ECC.ECCurve.Secp256r1),
                ECPoint.Parse("02df48f60e8f3e01c48ff40b9b7f1310d7a8b2a193188befe1c2e3df740e895093", Neo.Cryptography.ECC.ECCurve.Secp256r1),
                ECPoint.Parse("03b209fd4f53a7170ea4444e0cb0a6bb6a53c2bd016926989cf85f9b0fba17a70c", Neo.Cryptography.ECC.ECCurve.Secp256r1),
                ECPoint.Parse("03b8d9d5771d8f513aa0869b9cc8d50986403b78c6da36890638c3d46a5adce04a", Neo.Cryptography.ECC.ECCurve.Secp256r1)
            },
            MyIndex = -1
        };
        var testTx1 = TestUtils.CreateRandomHashTransaction();
        var testTx2 = TestUtils.CreateRandomHashTransaction();

        int txCountToInlcude = 256;
        consensusContext.TransactionHashes = new UInt256[txCountToInlcude];

        Transaction[] txs = new Transaction[txCountToInlcude];
        for (int i = 0; i < txCountToInlcude; i++)
        {
            txs[i] = TestUtils.CreateRandomHashTransaction();
            consensusContext.TransactionHashes[i] = txs[i].Hash;
        }
        // consensusContext.TransactionHashes = new UInt256[2] {testTx1.Hash, testTx2.Hash};
        consensusContext.Transactions = txs.ToDictionary(p => p.Hash);

        consensusContext.PreparationPayloads = new ExtensiblePayload[consensusContext.Validators.Length];
        var prepareRequestMessage = new PrepareRequest
        {
            PrevHash = consensusContext.Block.PrevHash,
            TransactionHashes = consensusContext.TransactionHashes,
            Timestamp = 23
        };
        consensusContext.PreparationPayloads[6] = MakeSignedPayload(consensusContext, prepareRequestMessage, 6, new[] { (byte)'3', (byte)'!' });
        consensusContext.PreparationPayloads[0] = MakeSignedPayload(consensusContext, new PrepareResponse { PreparationHash = consensusContext.PreparationPayloads[6].Hash }, 0, new[] { (byte)'t', (byte)'e' });
        consensusContext.PreparationPayloads[1] = MakeSignedPayload(consensusContext, new PrepareResponse { PreparationHash = consensusContext.PreparationPayloads[6].Hash }, 1, new[] { (byte)'s', (byte)'t' });
        consensusContext.PreparationPayloads[2] = null;
        consensusContext.PreparationPayloads[3] = MakeSignedPayload(consensusContext, new PrepareResponse { PreparationHash = consensusContext.PreparationPayloads[6].Hash }, 3, new[] { (byte)'1', (byte)'2' });
        consensusContext.PreparationPayloads[4] = null;
        consensusContext.PreparationPayloads[5] = null;

        consensusContext.CommitPayloads = new ExtensiblePayload[consensusContext.Validators.Length];
        using (SHA256 sha256 = SHA256.Create())
        {
            consensusContext.CommitPayloads[3] = MakeSignedPayload(consensusContext, new Commit { Signature = sha256.ComputeHash(testTx1.Hash.ToArray()).Concat(sha256.ComputeHash(testTx1.Hash.ToArray())).ToArray() }, 3, new[] { (byte)'3', (byte)'4' });
            consensusContext.CommitPayloads[6] = MakeSignedPayload(consensusContext, new Commit { Signature = sha256.ComputeHash(testTx2.Hash.ToArray()).Concat(sha256.ComputeHash(testTx2.Hash.ToArray())).ToArray() }, 3, new[] { (byte)'6', (byte)'7' });
        }

        consensusContext.Block.Header.Timestamp = TimeProvider.Current.UtcNow.ToTimestampMS();

        consensusContext.ChangeViewPayloads = new ExtensiblePayload[consensusContext.Validators.Length];
        consensusContext.ChangeViewPayloads[0] = MakeSignedPayload(consensusContext, new ChangeView { ViewNumber = 1, Timestamp = 6 }, 0, new[] { (byte)'A' });
        consensusContext.ChangeViewPayloads[1] = MakeSignedPayload(consensusContext, new ChangeView { ViewNumber = 1, Timestamp = 5 }, 1, new[] { (byte)'B' });
        consensusContext.ChangeViewPayloads[2] = null;
        consensusContext.ChangeViewPayloads[3] = MakeSignedPayload(consensusContext, new ChangeView { ViewNumber = 1, Timestamp = uint.MaxValue }, 3, new[] { (byte)'C' });
        consensusContext.ChangeViewPayloads[4] = null;
        consensusContext.ChangeViewPayloads[5] = null;
        consensusContext.ChangeViewPayloads[6] = MakeSignedPayload(consensusContext, new ChangeView { ViewNumber = 1, Timestamp = 1 }, 6, new[] { (byte)'D' });

        consensusContext.LastChangeViewPayloads = new ExtensiblePayload[consensusContext.Validators.Length];

        var copiedContext = TestUtils.CopyMsgBySerialization(consensusContext, new ConsensusContext(null, null, null));

        copiedContext.Block.PrevHash.Should().Be(consensusContext.Block.PrevHash);
        copiedContext.Block.Index.Should().Be(consensusContext.Block.Index);
        copiedContext.ViewNumber.Should().Be(consensusContext.ViewNumber);
        copiedContext.Validators.Should().BeEquivalentTo(consensusContext.Validators);
        copiedContext.MyIndex.Should().Be(consensusContext.MyIndex);
        copiedContext.Block.Header.PrimaryIndex.Should().Be(consensusContext.Block.Header.PrimaryIndex);
        copiedContext.Block.Timestamp.Should().Be(consensusContext.Block.Timestamp);
        copiedContext.Block.NextConsensus.Should().Be(consensusContext.Block.NextConsensus);
        copiedContext.TransactionHashes.Should().BeEquivalentTo(consensusContext.TransactionHashes);
        copiedContext.Transactions.Should().BeEquivalentTo(consensusContext.Transactions);
        copiedContext.Transactions.Values.Should().BeEquivalentTo(consensusContext.Transactions.Values);
        copiedContext.PreparationPayloads.Should().BeEquivalentTo(consensusContext.PreparationPayloads);
        copiedContext.CommitPayloads.Should().BeEquivalentTo(consensusContext.CommitPayloads);
        copiedContext.ChangeViewPayloads.Should().BeEquivalentTo(consensusContext.ChangeViewPayloads);
    }

    [TestMethod]
    public void TestSerializeAndDeserializeRecoveryMessageWithChangeViewsAndNoPrepareRequest()
    {
        var msg = new RecoveryMessage
        {
            ChangeViewMessages = new Dictionary<byte, RecoveryMessage.ChangeViewPayloadCompact>()
            {
                {
                    0,
                    new RecoveryMessage.ChangeViewPayloadCompact
                    {
                        ValidatorIndex = 0,
                        OriginalViewNumber = 9,
                        Timestamp = 6,
                        InvocationScript = new[] { (byte)'A' }
                    }
                },
                {
                    1,
                    new RecoveryMessage.ChangeViewPayloadCompact
                    {
                        ValidatorIndex = 1,
                        OriginalViewNumber = 7,
                        Timestamp = 5,
                        InvocationScript = new[] { (byte)'B' }
                    }
                },
                {
                    3,
                    new RecoveryMessage.ChangeViewPayloadCompact
                    {
                        ValidatorIndex = 3,
                        OriginalViewNumber = 5,
                        Timestamp = 3,
                        InvocationScript = new[] { (byte)'C' }
                    }
                },
                {
                    6,
                    new RecoveryMessage.ChangeViewPayloadCompact
                    {
                        ValidatorIndex = 6,
                        OriginalViewNumber = 2,
                        Timestamp = 1,
                        InvocationScript = new[] { (byte)'D' }
                    }
                }
            },
            PreparationHash = new UInt256(Crypto.Hash256(new[] { (byte)'a' })),
            PreparationMessages = new Dictionary<byte, RecoveryMessage.PreparationPayloadCompact>()
            {
                {
                    0,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 0,
                        InvocationScript = new[] { (byte)'t', (byte)'e' }
                    }
                },
                {
                    3,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 3,
                        InvocationScript = new[] { (byte)'1', (byte)'2' }
                    }
                },
                {
                    6,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 6,
                        InvocationScript = new[] { (byte)'3', (byte)'!' }
                    }
                }
            },
            CommitMessages = new Dictionary<byte, RecoveryMessage.CommitPayloadCompact>()
        };

        // msg.TransactionHashes = null;
        // msg.Nonce = 0;
        // msg.NextConsensus = null;
        // msg.MinerTransaction = (MinerTransaction) null;
        msg.PrepareRequestMessage.Should().Be(null);

        var copiedMsg = TestUtils.CopyMsgBySerialization(msg, new RecoveryMessage()); ;

        copiedMsg.ChangeViewMessages.Should().BeEquivalentTo(msg.ChangeViewMessages);
        copiedMsg.PreparationHash.Should().Be(msg.PreparationHash);
        copiedMsg.PreparationMessages.Should().BeEquivalentTo(msg.PreparationMessages);
        copiedMsg.CommitMessages.Count.Should().Be(0);
    }

    [TestMethod]
    public void TestSerializeAndDeserializeRecoveryMessageWithChangeViewsAndPrepareRequest()
    {
        Transaction[] txs = new Transaction[5];
        for (int i = 0; i < txs.Length; i++)
            txs[i] = TestUtils.CreateRandomHashTransaction();
        var msg = new RecoveryMessage
        {
            ChangeViewMessages = new Dictionary<byte, RecoveryMessage.ChangeViewPayloadCompact>()
            {
                {
                    0,
                    new RecoveryMessage.ChangeViewPayloadCompact
                    {
                        ValidatorIndex = 0,
                        OriginalViewNumber = 9,
                        Timestamp = 6,
                        InvocationScript = new[] { (byte)'A' }
                    }
                },
                {
                    1,
                    new RecoveryMessage.ChangeViewPayloadCompact
                    {
                        ValidatorIndex = 1,
                        OriginalViewNumber = 7,
                        Timestamp = 5,
                        InvocationScript = new[] { (byte)'B' }
                    }
                },
                {
                    3,
                    new RecoveryMessage.ChangeViewPayloadCompact
                    {
                        ValidatorIndex = 3,
                        OriginalViewNumber = 5,
                        Timestamp = 3,
                        InvocationScript = new[] { (byte)'C' }
                    }
                },
                {
                    6,
                    new RecoveryMessage.ChangeViewPayloadCompact
                    {
                        ValidatorIndex = 6,
                        OriginalViewNumber = 2,
                        Timestamp = 1,
                        InvocationScript = new[] { (byte)'D' }
                    }
                }
            },
            PrepareRequestMessage = new PrepareRequest
            {
                PrevHash = UInt256.Zero,
                TransactionHashes = txs.Select(p => p.Hash).ToArray()
            },
            PreparationHash = new UInt256(Crypto.Hash256(new[] { (byte)'a' })),
            PreparationMessages = new Dictionary<byte, RecoveryMessage.PreparationPayloadCompact>()
            {
                {
                    0,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 0,
                        InvocationScript = new[] { (byte)'t', (byte)'e' }
                    }
                },
                {
                    1,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 1,
                        InvocationScript = new[] { (byte)'s', (byte)'t' }
                    }
                },
                {
                    3,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 3,
                        InvocationScript = new[] { (byte)'1', (byte)'2' }
                    }
                }
            },
            CommitMessages = new Dictionary<byte, RecoveryMessage.CommitPayloadCompact>()
        };

        var copiedMsg = TestUtils.CopyMsgBySerialization(msg, new RecoveryMessage()); ;

        copiedMsg.ChangeViewMessages.Should().BeEquivalentTo(msg.ChangeViewMessages);
        copiedMsg.PrepareRequestMessage.Should().BeEquivalentTo(msg.PrepareRequestMessage);
        copiedMsg.PreparationHash.Should().Be(null);
        copiedMsg.PreparationMessages.Should().BeEquivalentTo(msg.PreparationMessages);
        copiedMsg.CommitMessages.Count.Should().Be(0);
    }

    [TestMethod]
    public void TestSerializeAndDeserializeRecoveryMessageWithoutChangeViewsWithoutCommits()
    {
        Transaction[] txs = new Transaction[5];
        for (int i = 0; i < txs.Length; i++)
            txs[i] = TestUtils.CreateRandomHashTransaction();
        var msg = new RecoveryMessage
        {
            ChangeViewMessages = new Dictionary<byte, RecoveryMessage.ChangeViewPayloadCompact>(),
            PrepareRequestMessage = new PrepareRequest
            {
                PrevHash = UInt256.Zero,
                TransactionHashes = txs.Select(p => p.Hash).ToArray()
            },
            PreparationMessages = new Dictionary<byte, RecoveryMessage.PreparationPayloadCompact>()
            {
                {
                    0,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 0,
                        InvocationScript = new[] { (byte)'t', (byte)'e' }
                    }
                },
                {
                    1,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 1,
                        InvocationScript = new[] { (byte)'s', (byte)'t' }
                    }
                },
                {
                    3,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 3,
                        InvocationScript = new[] { (byte)'1', (byte)'2' }
                    }
                },
                {
                    6,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 6,
                        InvocationScript = new[] { (byte)'3', (byte)'!' }
                    }
                }
            },
            CommitMessages = new Dictionary<byte, RecoveryMessage.CommitPayloadCompact>()
        };

        var copiedMsg = TestUtils.CopyMsgBySerialization(msg, new RecoveryMessage()); ;

        copiedMsg.ChangeViewMessages.Count.Should().Be(0);
        copiedMsg.PrepareRequestMessage.Should().BeEquivalentTo(msg.PrepareRequestMessage);
        copiedMsg.PreparationHash.Should().Be(null);
        copiedMsg.PreparationMessages.Should().BeEquivalentTo(msg.PreparationMessages);
        copiedMsg.CommitMessages.Count.Should().Be(0);
    }

    [TestMethod]
    public void TestSerializeAndDeserializeRecoveryMessageWithoutChangeViewsWithCommits()
    {
        Transaction[] txs = new Transaction[5];
        for (int i = 0; i < txs.Length; i++)
            txs[i] = TestUtils.CreateRandomHashTransaction();
        var msg = new RecoveryMessage
        {
            ChangeViewMessages = new Dictionary<byte, RecoveryMessage.ChangeViewPayloadCompact>(),
            PrepareRequestMessage = new PrepareRequest
            {
                PrevHash = UInt256.Zero,
                TransactionHashes = txs.Select(p => p.Hash).ToArray()
            },
            PreparationMessages = new Dictionary<byte, RecoveryMessage.PreparationPayloadCompact>()
            {
                {
                    0,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 0,
                        InvocationScript = new[] { (byte)'t', (byte)'e' }
                    }
                },
                {
                    1,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 1,
                        InvocationScript = new[] { (byte)'s', (byte)'t' }
                    }
                },
                {
                    3,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 3,
                        InvocationScript = new[] { (byte)'1', (byte)'2' }
                    }
                },
                {
                    6,
                    new RecoveryMessage.PreparationPayloadCompact
                    {
                        ValidatorIndex = 6,
                        InvocationScript = new[] { (byte)'3', (byte)'!' }
                    }
                }
            },
            CommitMessages = new Dictionary<byte, RecoveryMessage.CommitPayloadCompact>
            {
                {
                    1,
                    new RecoveryMessage.CommitPayloadCompact
                    {
                        ValidatorIndex = 1,
                        Signature = new byte[64] { (byte)'1', (byte)'2', 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                        InvocationScript = new[] { (byte)'1', (byte)'2' }
                    }
                },
                {
                    6,
                    new RecoveryMessage.CommitPayloadCompact
                    {
                        ValidatorIndex = 6,
                        Signature = new byte[64] { (byte)'3', (byte)'D', (byte)'R', (byte)'I', (byte)'N', (byte)'K', 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                        InvocationScript = new[] { (byte)'6', (byte)'7' }
                    }
                }
            }
        };

        var copiedMsg = TestUtils.CopyMsgBySerialization(msg, new RecoveryMessage()); ;

        copiedMsg.ChangeViewMessages.Count.Should().Be(0);
        copiedMsg.PrepareRequestMessage.Should().BeEquivalentTo(msg.PrepareRequestMessage);
        copiedMsg.PreparationHash.Should().Be(null);
        copiedMsg.PreparationMessages.Should().BeEquivalentTo(msg.PreparationMessages);
        copiedMsg.CommitMessages.Should().BeEquivalentTo(msg.CommitMessages);
    }

    private static ExtensiblePayload MakeSignedPayload(ConsensusContext context, ConsensusMessage message, byte validatorIndex, byte[] witnessInvocationScript)
    {
        message.BlockIndex = context.Block.Index;
        message.ValidatorIndex = validatorIndex;
        return new ExtensiblePayload
        {
            Category = "Consensus",
            ValidBlockStart = 0,
            ValidBlockEnd = message.BlockIndex,
            Sender = context.GetSender(validatorIndex),
            Data = message.ToArray(),
            Witness = new Witness
            {
                InvocationScript = witnessInvocationScript,
                VerificationScript = Contract.CreateSignatureRedeemScript(context.Validators[validatorIndex])
            }
        };
    }

    private StorageKey CreateStorageKeyForNativeNeo(byte prefix)
    {
        var keyData = new byte[sizeof(byte)];
        keyData[0] = prefix;

        var storageKey = new StorageKey
        {
            Id = NativeContract.NEO.Id,
            Key = keyData
        };

        return storageKey;
    }

    private void TellConsensusPayload(IActorRef actor, ExtensiblePayload payload)
    {
        actor.Tell(new Blockchain.RelayResult
        {
            Inventory = payload,
            Result = VerifyResult.Succeed
        });
    }
}
