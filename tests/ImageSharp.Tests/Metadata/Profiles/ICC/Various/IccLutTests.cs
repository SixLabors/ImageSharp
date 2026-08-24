// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.Various;

[Trait("Profile", "Icc")]
public class IccLutTests
{
    /// <summary>
    /// Gets lengths that exercise scalar execution, every SIMD width, mixed-width remainders, and multiple vectors.
    /// </summary>
    public static TheoryData<int> LutLengths => new()
    {
        0,
        1,
        7,
        8,
        9,
        15,
        16,
        17,
        31,
        32,
        33,
        63,
        64,
        65,
        255,
        256,
        257
    };

    /// <summary>
    /// Verifies that byte LUT construction preserves the scalar conversion result for every traversal shape.
    /// </summary>
    /// <param name="length">The number of LUT entries.</param>
    [Theory]
    [MemberData(nameof(LutLengths))]
    public void ByteConstructorMatchesScalarFormula(int length)
    {
        byte[] values = new byte[length];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (byte)((i * 73) + 19);
        }

        IccLut actual = new(values);

        Assert.Equal(values.Length, actual.Values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            float expected = values[i] / (float)byte.MaxValue;
            Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual.Values[i]));
        }
    }

    /// <summary>
    /// Verifies that unsigned-short LUT construction preserves the scalar conversion result for every traversal shape.
    /// </summary>
    /// <param name="length">The number of LUT entries.</param>
    [Theory]
    [MemberData(nameof(LutLengths))]
    public void UInt16ConstructorMatchesScalarFormula(int length)
    {
        ushort[] values = new ushort[length];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (ushort)((i * 12_347) + 1_019);
        }

        IccLut actual = new(values);

        Assert.Equal(values.Length, actual.Values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            float expected = values[i] / (float)ushort.MaxValue;
            Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual.Values[i]));
        }
    }

    /// <summary>
    /// Verifies bit-exact normalization for every possible unsigned-short input.
    /// </summary>
    [Fact]
    public void UInt16ConstructorMatchesScalarFormulaForEveryValue()
    {
        ushort[] values = new ushort[ushort.MaxValue + 1];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (ushort)i;
        }

        IccLut actual = new(values);

        for (int i = 0; i < values.Length; i++)
        {
            float expected = values[i] / (float)ushort.MaxValue;
            Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual.Values[i]));
        }
    }
}
