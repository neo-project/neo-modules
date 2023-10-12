
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Neo.Cryptography;
using Neo.IO;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using Array = System.Array;
using ECCurve = Neo.Cryptography.ECC.ECCurve;
using ECPoint = Neo.Cryptography.ECC.ECPoint;
namespace Neo.Consensus;

public static class TestBlockchain
{
    public static readonly NeoSystem TheNeoSystem;

    static TestBlockchain()
    {
        Console.WriteLine("initialize NeoSystem");
        TheNeoSystem = new NeoSystem(ProtocolSettings.Default, null, null);
    }

    public static void InitializeMockNeoSystem()
    {
    }

    internal static DataCache GetTestSnapshot()
    {
        return TheNeoSystem.GetSnapshot().CreateSnapshot();
    }
}

public class TestSetting : Neo.Consensus.Settings
{

    /// <summary>
    /// Have to make it this way since the GetConfiguration of Neo.Plugins is protected, can not be accessed from here.
    /// Ironically, all its contents are public.
    /// </summary>
    public TestSetting() : base(new ConfigurationBuilder().AddJsonFile("config.json", optional: true).Build().GetSection("ProtocolConfiguration"))
    {
    }
}

public class TestNeoSystem : NeoSystem
{
    public TestNeoSystem() : base(ProtocolSettings.Default)
    {
        // Console.WriteLine("initialize TestNeoSystem");
    }
}

public class TestWalletAccount : WalletAccount
{
    private static readonly KeyPair Key;

    public override bool HasKey => true;
    public override KeyPair GetKey() => Key;

    public TestWalletAccount(UInt160 hash)
        : base(hash, ProtocolSettings.Default)
    {
        var mock = new Mock<Contract>();
        mock.SetupGet(p => p.ScriptHash).Returns(hash);
        mock.Object.Script = Contract.CreateSignatureRedeemScript(Key.PublicKey);
        mock.Object.ParameterList = new[] { ContractParameterType.Signature };
        Contract = mock.Object;
    }

    static TestWalletAccount()
    {
        Random random = new();
        var prikey = new byte[32];
        random.NextBytes(prikey);
        Key = new KeyPair(prikey);
    }
}

public static class TestUtils
{
    public static readonly Random TestRandom = new Random(1337); // use fixed seed for guaranteed determinism

    public static ContractManifest CreateDefaultManifest()
    {
        return new ContractManifest()
        {
            Name = "testManifest",
            Groups = Array.Empty<ContractGroup>(),
            SupportedStandards = Array.Empty<string>(),
            Abi = new ContractAbi()
            {
                Events = Array.Empty<ContractEventDescriptor>(),
                Methods = new[]
                {
                        new ContractMethodDescriptor
                        {
                            Name = "testMethod",
                            Parameters = Array.Empty<ContractParameterDefinition>(),
                            ReturnType = ContractParameterType.Void,
                            Offset = 0,
                            Safe = true
                        }
                    }
            },
            Permissions = new[] { ContractPermission.DefaultPermission },
            Trusts = WildcardContainer<ContractPermissionDescriptor>.Create(),
            Extra = null
        };
    }

    public static ContractManifest CreateManifest(string method, ContractParameterType returnType, params ContractParameterType[] parameterTypes)
    {
        var manifest = CreateDefaultManifest();
        manifest.Abi.Methods = new ContractMethodDescriptor[]
        {
                new()
                {
                    Name = method,
                    Parameters = parameterTypes.Select((p, i) => new ContractParameterDefinition
                    {
                        Name = $"p{i}",
                        Type = p
                    }).ToArray(),
                    ReturnType = returnType
                }
        };
        return manifest;
    }

    public static StorageKey CreateStorageKey(this NativeContract contract, byte prefix, ISerializable key = null)
    {
        var k = new KeyBuilder(contract.Id, prefix);
        if (key != null) k = k.Add(key);
        return k;
    }

    public static StorageKey CreateStorageKey(this NativeContract contract, byte prefix, uint value)
    {
        return new KeyBuilder(contract.Id, prefix).AddBigEndian(value);
    }

    public static byte[] GetByteArray(int length, byte firstByte)
    {
        byte[] array = new byte[length];
        array[0] = firstByte;
        for (int i = 1; i < length; i++)
        {
            array[i] = 0x20;
        }
        return array;
    }

    public static NEP6Wallet GenerateTestWallet(string password)
    {
        JObject wallet = new JObject();
        wallet["name"] = "noname";
        wallet["version"] = new Version("1.0").ToString();
        wallet["scrypt"] = new ScryptParameters(2, 1, 1).ToJson();
        wallet["accounts"] = new JArray();
        wallet["extra"] = null;
        wallet.ToString().Should().Be("{\"name\":\"noname\",\"version\":\"1.0\",\"scrypt\":{\"n\":2,\"r\":1,\"p\":1},\"accounts\":[],\"extra\":null}");
        return new NEP6Wallet(null, password, ProtocolSettings.Default, wallet);
    }

    public static Transaction GetTransaction(UInt160 sender)
    {
        return new Transaction
        {
            Script = new[] { (byte)OpCode.PUSH2 },
            Attributes = Array.Empty<TransactionAttribute>(),
            Signers = new[]{ new Signer()
                {
                    Account = sender,
                    Scopes = WitnessScope.CalledByEntry,
                    AllowedContracts = Array.Empty<UInt160>(),
                    AllowedGroups = Array.Empty<ECPoint>(),
                    Rules = Array.Empty<WitnessRule>(),
                } },
            Witnesses = new[]{ new Witness
                {
                    InvocationScript = Array.Empty<byte>(),
                    VerificationScript = Array.Empty<byte>()
                } }
        };
    }

    internal static ContractState GetContract(string method = "test", int parametersCount = 0)
    {
        NefFile nef = new()
        {
            Compiler = "",
            Source = "",
            Tokens = Array.Empty<MethodToken>(),
            Script = new byte[] { 0x01, 0x01, 0x01, 0x01 }
        };
        nef.CheckSum = NefFile.ComputeChecksum(nef);
        return new ContractState
        {
            Id = 0x43000000,
            Nef = nef,
            Hash = nef.Script.Span.ToScriptHash(),
            Manifest = CreateManifest(method, ContractParameterType.Any, Enumerable.Repeat(ContractParameterType.Any, parametersCount).ToArray())
        };
    }

    internal static ContractState GetContract(byte[] script, ContractManifest manifest = null)
    {
        NefFile nef = new()
        {
            Compiler = "",
            Source = "",
            Tokens = Array.Empty<MethodToken>(),
            Script = script
        };
        nef.CheckSum = NefFile.ComputeChecksum(nef);
        return new ContractState
        {
            Id = 1,
            Hash = script.ToScriptHash(),
            Nef = nef,
            Manifest = manifest ?? CreateDefaultManifest()
        };
    }

    internal static StorageItem GetStorageItem(byte[] value)
    {
        return new StorageItem
        {
            Value = value
        };
    }

    internal static StorageKey GetStorageKey(int id, byte[] keyValue)
    {
        return new StorageKey
        {
            Id = id,
            Key = keyValue
        };
    }

    /// <summary>
    /// Test Util function SetupHeaderWithValues
    /// </summary>
    /// <param name="header">The header to be assigned</param>
    /// <param name="val256">PrevHash</param>
    /// <param name="merkRootVal">MerkleRoot</param>
    /// <param name="val160">NextConsensus</param>
    /// <param name="timestampVal">Timestamp</param>
    /// <param name="indexVal">Index</param>
    /// <param name="nonceVal">Nonce</param>
    /// <param name="scriptVal">Witness</param>
    public static void SetupHeaderWithValues(Header header, UInt256 val256, out UInt256 merkRootVal, out UInt160 val160, out ulong timestampVal, out ulong nonceVal, out uint indexVal, out Witness scriptVal)
    {
        header.PrevHash = val256;
        header.MerkleRoot = merkRootVal = UInt256.Parse("0x6226416a0e5aca42b5566f5a19ab467692688ba9d47986f6981a7f747bba2772");
        header.Timestamp = timestampVal = new DateTime(1980, 06, 01, 0, 0, 1, 001, DateTimeKind.Utc).ToTimestampMS(); // GMT: Sunday, June 1, 1980 12:00:01.001 AM
        header.Index = indexVal = 0;
        header.Nonce = nonceVal = 0;
        header.NextConsensus = val160 = UInt160.Zero;
        header.Witness = scriptVal = new Witness
        {
            InvocationScript = new byte[0],
            VerificationScript = new[] { (byte)OpCode.PUSH1 }
        };
    }

    public static void SetupBlockWithValues(Block block, UInt256 val256, out UInt256 merkRootVal, out UInt160 val160, out ulong timestampVal, out ulong nonceVal, out uint indexVal, out Witness scriptVal, out Transaction[] transactionsVal, int numberOfTransactions)
    {
        Header header = new Header();
        SetupHeaderWithValues(header, val256, out merkRootVal, out val160, out timestampVal, out nonceVal, out indexVal, out scriptVal);

        transactionsVal = new Transaction[numberOfTransactions];
        if (numberOfTransactions > 0)
        {
            for (int i = 0; i < numberOfTransactions; i++)
            {
                transactionsVal[i] = GetTransaction(UInt160.Zero);
            }
        }

        block.Header = header;
        block.Transactions = transactionsVal;

        header.MerkleRoot = merkRootVal = MerkleTree.ComputeRoot(block.Transactions.Select(p => p.Hash).ToArray());
    }

    public static Transaction CreateRandomHashTransaction()
    {
        var randomBytes = new byte[16];
        TestRandom.NextBytes(randomBytes);
        return new Transaction
        {
            Script = randomBytes,
            Attributes = Array.Empty<TransactionAttribute>(),
            Signers = new Signer[] { new Signer() { Account = UInt160.Zero } },
            Witnesses = new[]
            {
                    new Witness
                    {
                        InvocationScript = new byte[0],
                        VerificationScript = new byte[0]
                    }
                }
        };
    }

    public static T CopyMsgBySerialization<T>(T serializableObj, T newObj) where T : ISerializable
    {
        MemoryReader reader = new(serializableObj.ToArray());
        newObj.Deserialize(ref reader);
        return newObj;
    }

    public static bool EqualsTo(this StorageItem item, StorageItem other)
    {
        return item.Value.Span.SequenceEqual(other.Value.Span);
    }
}

public class UT_Crypto
{
    // private KeyPair key = null;

    public static KeyPair generateKey(int privateKeyLength)
    {
        byte[] privateKey = new byte[privateKeyLength];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(privateKey);
        }
        return new KeyPair(privateKey);
    }

    public static KeyPair generateCertainKey(int privateKeyLength)
    {
        byte[] privateKey = new byte[privateKeyLength];
        for (int i = 0; i < privateKeyLength; i++)
        {
            privateKey[i] = (byte)((byte)i % byte.MaxValue);
        }
        return new KeyPair(privateKey);
    }
}

public class TestTimeProvider
{

    private static readonly TestTimeProvider Default = new TestTimeProvider();

    /// <summary>
    /// The currently used <see cref="T:Neo.TimeProvider" /> instance.
    /// </summary>
    public static TestTimeProvider Current { get; internal set; } = TestTimeProvider.Default;

    /// <summary>
    /// Gets the current time expressed as the Coordinated Universal Time (UTC).
    /// </summary>
    public virtual DateTime UtcNow => DateTime.UtcNow;

    internal static void ResetToDefault() => TestTimeProvider.Current = TestTimeProvider.Default;
}

public class TestCachedCommittee : TestInteroperableList<(ECPoint PublicKey, BigInteger Votes)>
{
    public TestCachedCommittee()
    {
    }

    public TestCachedCommittee(IEnumerable<(ECPoint, BigInteger)> collection) => this.AddRange(collection);

    protected override (ECPoint, BigInteger) ElementFromStackItem(StackItem item)
    {
        Struct @struct = (Struct)item;
        return (ECPoint.DecodePoint(@struct[0].GetSpan(), ECCurve.Secp256r1), @struct[1].GetInteger());
    }

    protected override StackItem ElementToStackItem(
        (ECPoint PublicKey, BigInteger Votes) element,
        ReferenceCounter referenceCounter)
    {
        Struct stackItem = new Struct(referenceCounter);
        stackItem.Add((StackItem)element.PublicKey.ToArray());
        stackItem.Add((StackItem)element.Votes);
        return (StackItem)stackItem;
    }
}


public abstract class TestInteroperableList<T> :
    IList<T>,
    ICollection<T>,
    IEnumerable<T>,
    IEnumerable,
    IInteroperable
{
    private System.Collections.Generic.List<T> list;

    private System.Collections.Generic.List<T> List => this.list ?? (this.list = new System.Collections.Generic.List<T>());

    public T this[int index]
    {
        get => this.List[index];
        set => this.List[index] = value;
    }

    public int Count => this.List.Count;

    public bool IsReadOnly => false;

    public void Add(T item) => this.List.Add(item);

    public void AddRange(IEnumerable<T> collection) => this.List.AddRange(collection);

    public void Clear() => this.List.Clear();

    public bool Contains(T item) => this.List.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => this.List.CopyTo(array, arrayIndex);

    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)this.List.GetEnumerator();

    public IEnumerator<T> GetEnumerator() => (IEnumerator<T>)this.List.GetEnumerator();

    public int IndexOf(T item) => this.List.IndexOf(item);

    public void Insert(int index, T item) => this.List.Insert(index, item);

    public bool Remove(T item) => this.List.Remove(item);

    public void RemoveAt(int index) => this.List.RemoveAt(index);

    public void Sort() => this.List.Sort();

    protected abstract T ElementFromStackItem(StackItem item);

    protected abstract StackItem ElementToStackItem(T element, ReferenceCounter referenceCounter);

    public void FromStackItem(StackItem stackItem)
    {
        this.List.Clear();
        foreach (StackItem stackItem1 in (Neo.VM.Types.Array)stackItem)
            this.Add(this.ElementFromStackItem(stackItem1));
    }

    public StackItem ToStackItem(ReferenceCounter referenceCounter) => (StackItem)new Neo.VM.Types.Array(referenceCounter, this.Select<T, StackItem>((Func<T, StackItem>)(p => this.ElementToStackItem(p, referenceCounter))));
}
