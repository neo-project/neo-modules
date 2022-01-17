namespace Neo.Consensus.DKG;

using System;
using System.Collections.Generic;
using System.Text;

public struct Fraction
{
    //Coefficients
    public uint numerator;
    public uint denominator;
    public bool sign;
    //ID
    public uint id;
}

public class Utility
{
    public static uint GetGCD(uint m, uint n)
    {
        uint result = 0;
        while (n != 0)
        {
            result = m % n;
            m = n;
            n = result;
        }

        return m;
    }

    public static uint GetLCM(uint m, uint n)
    {
        return m * (n / GetGCD(m, n));
    }

    public static uint GetLCM(uint[] input)
    {
        if (input == null || input.Length == 0) return 0;
        if (input.Length == 1) return input[0];
        uint result = GetLCM(input[0], input[1]);
        for (int i = 2; i < input.Length; i++)
        {
            result = GetLCM(result, input[i]);
        }

        return result;
    }

    public static Fraction[] GetCoefficient(bool[] input)
    {
        List<uint> convertedInput = new List<uint>();
        for (uint i = 0; i < input.Length; i++)
        {
            if (input[i]) convertedInput.Add(i + 1);
        }

        return GetCoefficient(convertedInput.ToArray());
    }

    public static Fraction[] GetCoefficient(uint[] input)
    {
        if (input == null || input.Length == 0) return null;
        Array.Sort(input);
        uint product = input[0];
        if (product == 0) return null;
        for (int i = 1; i < input.Length; i++)
        {
            if (input[i] == 0 || input[i - 1] == input[i]) return null;
            product *= input[i];
        }

        Fraction[] result = new Fraction[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            result[i] = new Fraction();
            result[i].numerator = product / input[i];
            result[i].denominator = 1;
            for (int j = 0; j < input.Length; j++)
            {
                if (j == i) continue;
                result[i].denominator *= i < j ? input[j] - input[i] : input[i] - input[j];
            }

            uint gcd = GetGCD(result[i].numerator, result[i].denominator);
            result[i].numerator /= gcd;
            result[i].denominator /= gcd;
            result[i].sign = i % 2 == 0;
            result[i].id = input[i] - 1;
        }

        return result;
    }

    public static int GetZeroBits(uint n, uint m)
    {
        return (int)(Math.Log2(n * ((long)Math.Pow(n, m) - 1) / (n - 1) + 1) + 3);
    }

    public static List<Fraction[]> GetAllFractions(uint n, uint m)
    {
        List<Fraction[]> fractions = new List<Fraction[]>();
        if (m == 0 || n == 0 || m > n)
        {
            return fractions;
        }

        bool[] comp = new bool[n];
        int q = 0;
        for (; q < m; q++)
        {
            comp[q] = true;
        }

        fractions.Add(GetCoefficient(comp));

        while (true)
        {
            for (q = 0; q < n - 1; q++)
            {
                if (comp[q] == true && comp[q + 1] == false) break;
            }

            if (q == n - 1) break;
            comp[q] = false;
            comp[q + 1] = true;

            int p = 0;
            while (p < q)
            {
                while (p < n - 1 && comp[p] == true) p++;
                while (q > 0 && comp[q] == false) q--;
                if (p < q)
                {
                    comp[p] = true;
                    comp[q] = false;
                }
            }

            fractions.Add(GetCoefficient(comp));
        }

        return fractions;
    }
}
