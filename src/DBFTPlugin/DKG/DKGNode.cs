namespace Neo.Consensus.DKG;

using Cortex.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;

public class DKGNode
{
    private readonly uint n = 0;
    private readonly uint m = 0;
    private readonly byte[] domain;

    //Local secret key to generate shared private keys
    public List<byte[]> blsSecretKeys = new();
    public List<byte[]> dkgSecretKeys = new();
    // Shared private keys from peers to generate aggregated key
    private byte[][] collectedSharedPrivateKeys;
    private byte[][] collectedSharedPublicKeys;

    // Locally aggregated private key to sign message
    private byte[] LocalAggregatedPrivateKey => GetAggregatePrivateKey();
    public byte[] LocalAggregatedPublicKey => GetAggregatedPublicKey();

    private byte[][] aggregatedPublicKeysForSignature;
    private uint[][] commonWeightSet;

    public int Index { get; } = -1;

    public DKGNode(int index, uint n, uint m)
    {
        Index = index;
        this.n = n;
        this.m = m;

        commonWeightSet = new uint[n][];
        for (int i = 0; i < this.n; i++)
        {
            commonWeightSet[i] = new uint[m];
            commonWeightSet[i][0] = 1;
            for (int j = 1; j < m; j++)
            {
                commonWeightSet[i][j] = (uint)(commonWeightSet[i][j - 1] * (i + 1));
            }
        }


        var keys = GeneratePrivateKeys(n, m);

        foreach (var key in keys)
        {
            blsSecretKeys.Add(key);
        }
        var dkgkeys = GenerateSharedPrivateKeys();
        foreach (var key in dkgkeys) dkgSecretKeys.Add(key);
        collectedSharedPrivateKeys = new byte[n][];
        collectedSharedPublicKeys = new byte[n][];

        aggregatedPublicKeysForSignature = new byte[n][];


    }

    private byte[] GeneratePrivateKeyFromRandom(int bits)
    {
        int bytes = bits / 8;
        int ints = bytes / sizeof(int);
        int start = ints * sizeof(int);
        int remainder = bits % 8;
        if (bytes >= BLSHerumi.PrivateKeyLength - 1)
        {
            throw new ArithmeticException("Cannot generate private key!");
        }

        Random rand = new Random();
        byte[] privateKey = new byte[ints * sizeof(int)]; //In case that aggregated private keys surpass limit
        for (int i = 0; i < BLSHerumi.PrivateKeyLength / sizeof(int) - ints; i++)
        {
            privateKey = privateKey.Concat(BitConverter.GetBytes(rand.Next())).ToArray();
        }

        for (int i = start; i < bytes; i++)
        {
            privateKey[i] = 0;
        }

        privateKey[bytes] >>= remainder;
        return privateKey;
    }

    private byte[][] GeneratePrivateKeys(uint n, uint m)
    {
        byte[][] privateKeys = new byte[m][]; //serial, data

        int bits = Utility.GetZeroBits(n, m);
        for (int j = 0; j < m; j++)
        {
            privateKeys[j] = GeneratePrivateKeyFromRandom(bits);
        }

        return privateKeys;
    }

    private byte[] AggregatePrivateKey(byte[][] privateKeys, uint[] weight)
    {
        if (privateKeys == null || privateKeys.Length == 0) return null;
        if (weight == null || weight.Length == 0) return null;
        if (privateKeys.Length != weight.Length) return null;

        byte[] result = new byte[privateKeys[0].Length];
        for (int j = 0; j < privateKeys.Length; j++)
        {
            for (int i = result.Length - 1; i >= 0; i--)
            {
                uint roughResult = result[i] + privateKeys[j][i] * weight[j];
                result[i] = (byte)roughResult;
                uint flap = roughResult / 256;
                int p = i - 1;
                while (flap != 0)
                {
                    if (p < 0) throw new ArithmeticException("Private key exceeds limit!");
                    flap += result[p];
                    result[p--] = (byte)flap;
                    flap /= 256;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Generate shared private key
    /// </summary>
    /// <param name="commonWeightSet"></param>
    /// <returns></returns>
    public byte[][] GenerateSharedPrivateKeys()
    {
        //Calculate shared private keys
        var sharedPrivateKeys = new byte[n][];
        for (int i = 0; i < n; i++)
        {
            var key = AggregatePrivateKey(this.blsSecretKeys.ToArray(), commonWeightSet[i]);
            sharedPrivateKeys[i] = key;
        }

        return sharedPrivateKeys;
    }

    public void CollectSharedKeyPair(int index, byte[] sharedPrivateKey, byte[] sharedPublicKey)
    {
        if (this.collectedSharedPrivateKeys[index] != null &&
            this.collectedSharedPublicKeys[index] != null) return;
        this.collectedSharedPrivateKeys[index] = sharedPrivateKey;
        this.collectedSharedPublicKeys[index] = sharedPublicKey;
    }

    /// <summary>
    /// Get BLS aggregated public key from peer
    /// </summary>
    /// <param name="index"></param>
    /// <param name="aggretatedPublicKey"></param>
    public void CollectPublicKeyFromPeerForSignature(int index, byte[] aggretatedPublicKey)
    {
        if (this.aggregatedPublicKeysForSignature[index] != null) return;
        this.aggregatedPublicKeysForSignature[index] = aggretatedPublicKey;
    }

    /// <summary>
    /// Calculate aggregated privateKeys which are used to construct signatures
    /// </summary>
    /// <returns></returns>
    public byte[] GetAggregatePrivateKey()
    {
        uint[] weightSet = new uint[n];
        for (int k = 0; k < n; k++)
        {
            weightSet[k] = 1u;
        }

        return AggregatePrivateKey(collectedSharedPrivateKeys.ToArray(), weightSet);
    }

    private byte[][] GetPublishedPubicKeys()
    {
        var blsPublicKeys = new byte[m][];
        for (int i = 0; i < m; i++)
        {
            using var blsPublic = new BLSHerumi(new BLSParameters() { PrivateKey = blsSecretKeys[i] });
            var pub_key = new byte[BLSHerumi.PublicKeyLength];

            _ = blsPublic.TryExportBLSPublicKey(pub_key, out var _);
            blsPublicKeys[i] = pub_key;
        }

        return blsPublicKeys;
    }

    public byte[][] GetSharedPublicKeys(uint[][] commonWeightSet)
    {
        var blsPublicKeys = GetPublishedPubicKeys();
        var sharedPublicKeys = new byte[n][];
        for (int i = 0; i < n; i++)
        {
            var contractedPublicKeys = new Span<byte>(new byte[BLSHerumi.PublicKeyLength * m]);
            for (int k = 0; k < m; k++)
            {
                blsPublicKeys.ToArray()[k].CopyTo(contractedPublicKeys.Slice(k * BLSHerumi.PublicKeyLength));
            }

            //blsPublicKeys.Select((p,i) => p.CopyTo(contractedPublicKeys.Slice(i * BLSHerumi.PublicKeyLength)));
            using var blsAggregateKeys = new BLSHerumi(new BLSParameters());
            sharedPublicKeys[i] = new byte[BLSHerumi.PublicKeyLength];
            blsAggregateKeys.TryAggregatePublicKeys(contractedPublicKeys, commonWeightSet[i], sharedPublicKeys[i],
                out var _);
        }

        return sharedPublicKeys;
    }

    public byte[] GetAggregatedPublicKey()
    {
        var contractedPublicKeys = new Span<byte>(new byte[BLSHerumi.PublicKeyLength * n]);
        uint[] weightSet = new uint[n];
        for (int k = 0; k < n; k++)
        {
            this.collectedSharedPublicKeys[k].CopyTo(contractedPublicKeys.Slice(k * BLSHerumi.PublicKeyLength));
            weightSet[k] = 1u;
        }

        using var blsAggregateKeys = new BLSHerumi(new BLSParameters());
        var localAggregatedPublicKey = new byte[BLSHerumi.PublicKeyLength];
        blsAggregateKeys.TryAggregatePublicKeys(contractedPublicKeys, weightSet, localAggregatedPublicKey, out var _);
        return localAggregatedPublicKey;
    }

    public byte[] GetSignature(byte[] msg)
    {
        using var blsSign = new BLSHerumi(new BLSParameters() { PrivateKey = LocalAggregatedPrivateKey });
        var signature = new byte[BLSHerumi.SignatureLength];

        _ = blsSign.TrySignHash(msg, signature.AsSpan(), out var _, this.domain);

        var aggregatePublicKeyParameters = new BLSParameters()
        {
            PublicKey = LocalAggregatedPublicKey
        };
        using var blsVerify = new BLSHerumi(aggregatePublicKeyParameters);
        if (!blsVerify.VerifyHash(msg, signature, domain))
        {
            throw new Exception("AggregatePrivateKeys verification failed!");
        }

        return signature;
    }

    public void CheckKeyPair(byte[] msg, byte[] sharedPrivateKey, byte[] sharedPublicKey)
    {
        using var blsSign = new BLSHerumi(new BLSParameters() { PrivateKey = sharedPrivateKey });
        var signature = new byte[BLSHerumi.SignatureLength];
        _ = blsSign.TrySignHash(msg, signature.AsSpan(), out var _, domain);

        var aggregatePublicKeyParameters = new BLSParameters()
        {
            PublicKey = sharedPublicKey
        };
        using var blsVerify = new BLSHerumi(aggregatePublicKeyParameters);
        if (!blsVerify.VerifyHash(msg, signature, domain))
        {
            throw new Exception("SharedPrivateKeys verification failed!");
        }
        //Console.WriteLine("Shared private keys verified...");
    }
}
