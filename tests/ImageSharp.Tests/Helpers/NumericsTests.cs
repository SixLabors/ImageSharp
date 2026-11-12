// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Helpers;

public class NumericsTests
{
    private readonly ApproximateFloatComparer approximateFloatComparer = new(1e-6f);

    /// <summary>
    /// Gets lengths that straddle the Vector4 block boundaries used by the 128-, 256-, and 512-bit kernels.
    /// </summary>
    public static TheoryData<int> AssociationSpanLengths => new() { 0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 31, 32, 33, 63 };

    /// <summary>
    /// Verifies that component replacement preserves every selected and retained float bit on all SIMD paths.
    /// </summary>
    [Fact]
    public void BulkComponentReplacementPreservesBits()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateBulkComponentReplacement,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies that each replacement operation rejects the span that cannot cover its required component count.
    /// </summary>
    [Fact]
    public void BulkComponentReplacementRequiresEnoughStorage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Numerics.WithW(new Vector4[33], new float[32]));
        Assert.Throws<ArgumentException>(() => Numerics.WithXYZ(new Vector3[33], new Vector4[32]));
    }

    /// <summary>
    /// Checks direct component selection, including NaN payloads, infinities and both signs of zero.
    /// </summary>
    private static void ValidateBulkComponentReplacement()
    {
        // Every lane has an independent period. These values are data, not arithmetic operands:
        // replacing another component must retain the exact IEEE representation, including NaN payloads.
        ReadOnlySpan<uint> patterns = [0x00000000U, 0x80000000U, 0x7FC12345U, 0xFFC54321U, 0x7F800000U, 0xFF800000U, 0x3F400000U];
        uint[] originalBits = new uint[33 * 4];
        Vector3[] xyz = new Vector3[33];
        float[] w = new float[34];
        Vector4[] storage = new Vector4[35];
        storage.AsSpan().Fill(new Vector4(43F));
        Span<Vector4> destination = storage.AsSpan(1, xyz.Length);
        for (int i = 0; i < xyz.Length; i++)
        {
            for (int component = 0; component < 4; component++)
            {
                originalBits[(i * 4) + component] = patterns[(i + (2 * component)) % patterns.Length];
            }

            xyz[i] = new(
                BitConverter.UInt32BitsToSingle(patterns[(i + 1) % patterns.Length]),
                BitConverter.UInt32BitsToSingle(patterns[(i + 3) % patterns.Length]),
                BitConverter.UInt32BitsToSingle(patterns[(i + 5) % patterns.Length]));

            w[i] = BitConverter.UInt32BitsToSingle(patterns[(i + 1) % patterns.Length]);
        }

        MemoryMarshal.Cast<uint, Vector4>(originalBits).CopyTo(destination);
        uint[] expected = originalBits.AsSpan().ToArray();
        for (int i = 0; i < xyz.Length; i++)
        {
            expected[(i * 4) + 3] = patterns[(i + 1) % patterns.Length];
        }

        // Surplus W input is legal. Only the destination count determines the replacement count.
        Numerics.WithW(destination, w);
        Assert.Equal(expected, MemoryMarshal.Cast<Vector4, uint>(destination).ToArray());

        ReadOnlySpan<uint> constantBits = [0x3F800000U, 0x80000000U, 0x7FC12345U];
        foreach (uint bits in constantBits)
        {
            for (int i = 0; i < xyz.Length; i++)
            {
                expected[(i * 4) + 3] = bits;
            }

            Numerics.WithW(destination, BitConverter.UInt32BitsToSingle(bits));
            Assert.Equal(expected, MemoryMarshal.Cast<Vector4, uint>(destination).ToArray());
        }

        MemoryMarshal.Cast<uint, Vector4>(originalBits).CopyTo(destination);
        expected = originalBits.AsSpan().ToArray();
        for (int i = 0; i < xyz.Length; i++)
        {
            expected[i * 4] = patterns[(i + 1) % patterns.Length];
            expected[(i * 4) + 1] = patterns[(i + 3) % patterns.Length];
            expected[(i * 4) + 2] = patterns[(i + 5) % patterns.Length];
        }

        // Surplus destination capacity is legal for XYZ replacement. Its last vector must remain untouched.
        Numerics.WithXYZ(xyz, storage.AsSpan(1));
        Assert.Equal(expected, MemoryMarshal.Cast<Vector4, uint>(destination).ToArray());
        Assert.Equal(new Vector4(43F), storage[0]);
        Assert.Equal(new Vector4(43F), storage[^1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(100)]
    [InlineData(123)]
    [InlineData(53436353)]
    public void Modulo2(int x)
    {
        int actual = Numerics.Modulo2(x);
        Assert.Equal(x % 2, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(100)]
    [InlineData(123)]
    [InlineData(53436353)]
    public void Modulo4(int x)
    {
        int actual = Numerics.Modulo4(x);
        Assert.Equal(x % 4, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(100)]
    [InlineData(123)]
    [InlineData(53436353)]
    [InlineData(975)]
    public void Modulo8(int x)
    {
        int actual = Numerics.Modulo8(x);
        Assert.Equal(x % 8, actual);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(0, 4)]
    [InlineData(3, 4)]
    [InlineData(5, 4)]
    [InlineData(5, 8)]
    [InlineData(8, 8)]
    [InlineData(8, 16)]
    [InlineData(15, 16)]
    [InlineData(17, 16)]
    [InlineData(17, 32)]
    [InlineData(31, 32)]
    [InlineData(32, 32)]
    [InlineData(33, 32)]
    public void Modulo2P(int x, int m)
    {
        int actual = Numerics.ModuloP2(x, m);
        Assert.Equal(x % m, actual);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-17)]
    [InlineData(-12856)]
    [InlineData(-32)]
    [InlineData(-7425)]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(12856)]
    [InlineData(32)]
    [InlineData(7425)]
    public void Abs(int x)
    {
        int expected = Math.Abs(x);
        Assert.Equal(expected, Numerics.Abs(x));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-17)]
    [InlineData(-12856)]
    [InlineData(-32)]
    [InlineData(-7425)]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(12856)]
    [InlineData(32)]
    [InlineData(7425)]
    public void Pow2(float x)
    {
        float expected = (float)Math.Pow(x, 2);
        Assert.Equal(expected, Numerics.Pow2(x));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-17)]
    [InlineData(-12856)]
    [InlineData(-32)]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(12856)]
    [InlineData(32)]
    public void Pow3(float x)
    {
        float expected = (float)Math.Pow(x, 3);
        Assert.Equal(expected, Numerics.Pow3(x));
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 42, 1)]
    [InlineData(10, 8, 2)]
    [InlineData(12, 18, 6)]
    [InlineData(4536, 1000, 8)]
    [InlineData(1600, 1024, 64)]
    public void GreatestCommonDivisor(int a, int b, int expected)
    {
        int actual = Numerics.GreatestCommonDivisor(a, b);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 42, 42)]
    [InlineData(3, 4, 12)]
    [InlineData(6, 4, 12)]
    [InlineData(1600, 1024, 25600)]
    [InlineData(3264, 100, 81600)]
    public void LeastCommonMultiple(int a, int b, int expected)
    {
        int actual = Numerics.LeastCommonMultiple(a, b);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(AssociationSpanLengths))]
    public void PremultiplyVectorSpan(int length)
    {
        Random rnd = new(42);
        Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);

        for (int i = 0; i < source.Length; i++)
        {
            // Exact zero and one exercise the special alpha boundaries alongside the random fractional values.
            source[i].W = i % 5 switch
            {
                0 => 0F,
                1 => 1F,
                _ => source[i].W
            };
        }

        Vector4[] expected =
        [
            .. source.Select(v =>
            {
                v = Numerics.Premultiply(v.AsVector128()).AsVector4();
                return v;
            })
        ];

        Numerics.Premultiply(source);

        Assert.Equal(expected, source, this.approximateFloatComparer);

        for (int i = 0; i < source.Length; i++)
        {
            // Alpha is storage metadata here and must survive each SIMD width without any floating-point transformation.
            Assert.Equal(BitConverter.SingleToInt32Bits(expected[i].W), BitConverter.SingleToInt32Bits(source[i].W));
        }
    }

    [Theory]
    [MemberData(nameof(AssociationSpanLengths))]
    public void UnPremultiplyVectorSpan(int length)
    {
        Random rnd = new(42);
        Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);

        for (int i = 0; i < source.Length; i++)
        {
            // A zero alpha preserves the complete source vector by contract; one also verifies the identity case.
            source[i].W = i % 5 switch
            {
                0 => 0F,
                1 => 1F,
                _ => source[i].W
            };
        }

        Vector4[] expected =
        [
            .. source.Select(v =>
            {
                v = Numerics.UnPremultiply(v.AsVector128()).AsVector4();
                return v;
            })
        ];

        Numerics.UnPremultiply(source);

        Assert.Equal(expected, source, this.approximateFloatComparer);

        for (int i = 0; i < source.Length; i++)
        {
            // Alpha is the divisor and must still survive each SIMD width without any floating-point transformation.
            Assert.Equal(BitConverter.SingleToInt32Bits(expected[i].W), BitConverter.SingleToInt32Bits(source[i].W));
        }
    }
}
