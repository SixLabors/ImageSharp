// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

/// <summary>
/// Verifies the shared contract for pixel formats that store associated alpha.
/// </summary>
/// <typeparam name="TPixel">The associated-alpha pixel format.</typeparam>
[Trait("Category", "PixelFormats")]
public abstract class AssociatedAlphaPixelTests<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the color channels described by the pixel format.
    /// </summary>
    protected virtual PixelColorType ExpectedColorType => PixelColorType.RGB | PixelColorType.Alpha;

    [Fact]
    public void PixelInformationDescribesAssociatedAlpha()
    {
        PixelTypeInfo info = TPixel.GetPixelTypeInfo();
        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        int expectedComponentPrecision = (Unsafe.SizeOf<TPixel>() * 8) / 4;

        Assert.Equal(Unsafe.SizeOf<TPixel>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Associated, info.AlphaRepresentation);
        Assert.Equal(this.ExpectedColorType, info.ColorType);
        Assert.Equal(4, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);

        for (int i = 0; i < componentInfo.ComponentCount; i++)
        {
            Assert.Equal(expectedComponentPrecision, componentInfo.GetComponentPrecision(i));
        }
    }
}

/// <summary>
/// Tests the <see cref="Rgba32P"/> pixel format.
/// </summary>
public class Rgba32PTests : AssociatedAlphaPixelTests<Rgba32P>
{
    [Fact]
    public void ByteLayoutAndPackedValue()
    {
        Rgba32P[] pixels = [new(1, 2, 3, 4)];

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
        Assert.Equal(0x04030201U, pixels[0].PackedValue);
    }
}

/// <summary>
/// Tests the <see cref="Bgra32P"/> pixel format.
/// </summary>
public class Bgra32PTests : AssociatedAlphaPixelTests<Bgra32P>
{
    /// <inheritdoc />
    protected override PixelColorType ExpectedColorType => PixelColorType.BGR | PixelColorType.Alpha;

    [Fact]
    public void ByteLayoutAndPackedValue()
    {
        Bgra32P[] pixels = [new(1, 2, 3, 4)];

        Assert.Equal(new byte[] { 3, 2, 1, 4 }, MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
        Assert.Equal(0x04010203U, pixels[0].PackedValue);
    }
}

/// <summary>
/// Tests the <see cref="Argb32P"/> pixel format.
/// </summary>
public class Argb32PTests : AssociatedAlphaPixelTests<Argb32P>
{
    [Fact]
    public void ByteLayoutAndPackedValue()
    {
        Argb32P[] pixels = [new(1, 2, 3, 4)];

        Assert.Equal(new byte[] { 4, 1, 2, 3 }, MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
        Assert.Equal(0x03020104U, pixels[0].PackedValue);
    }
}

/// <summary>
/// Tests the <see cref="Abgr32P"/> pixel format.
/// </summary>
public class Abgr32PTests : AssociatedAlphaPixelTests<Abgr32P>
{
    /// <inheritdoc />
    protected override PixelColorType ExpectedColorType => PixelColorType.BGR | PixelColorType.Alpha;

    [Fact]
    public void ByteLayoutAndPackedValue()
    {
        Abgr32P[] pixels = [new(1, 2, 3, 4)];

        Assert.Equal(new byte[] { 4, 3, 2, 1 }, MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
        Assert.Equal(0x01020304U, pixels[0].PackedValue);
    }
}

/// <summary>
/// Tests the <see cref="NormalizedByte4P"/> pixel format.
/// </summary>
public class NormalizedByte4PTests : AssociatedAlphaPixelTests<NormalizedByte4P>
{
    [Fact]
    public void PackedValueMatchesNormalizedByte4ForAssociatedVector()
    {
        Vector4 associated = new(.1F, -.3F, .5F, -.7F);

        Assert.Equal(new NormalizedByte4(associated).PackedValue, new NormalizedByte4P(associated).PackedValue);
    }

    [Fact]
    public void MinimumStorageCodeDecodesAsNegativeOne()
    {
        NormalizedByte4P pixel = new() { PackedValue = 0x80808080 };

        Assert.Equal(-Vector4.One, pixel.ToVector4());
        Assert.Equal(Vector4.Zero, pixel.ToScaledVector4());
        Assert.Equal(-Vector4.One, pixel.ToUnassociatedVector4());
        Assert.Equal(Vector4.Zero, pixel.ToUnassociatedScaledVector4());
    }

    [Fact]
    public void AssociatedScaledVectorsMatchUnassociatedVectorsWithinTwoUlpsForEveryValidComponentAndAlpha()
    {
        for (int alpha = 0; alpha < byte.MaxValue; alpha++)
        {
            for (int associated = 0; associated <= alpha; associated++)
            {
                uint component = (byte)(associated - 127);
                uint alphaComponent = (byte)(alpha - 127);
                NormalizedByte4P pixel = new() { PackedValue = component | (component << 8) | (component << 16) | (alphaComponent << 24) };
                Vector4 expected = pixel.ToUnassociatedScaledVector4();
                expected.X *= expected.W;
                expected.Y *= expected.W;
                expected.Z *= expected.W;
                Vector4 actual = pixel.ToAssociatedScaledVector4();

                // All 32,640 valid storage pairs are covered. Two ULP is the measured maximum from the independently ordered division and multiplication.
                AlphaRepresentationTestAssertions.EqualWithinTwoUlps(expected, actual);
                Assert.Equal(BitConverter.SingleToInt32Bits(expected.W), BitConverter.SingleToInt32Bits(actual.W));
            }
        }
    }

    [Fact]
    public void BulkConversionsMatchScalarAcrossHardwareWidths() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertBulkConversionsMatchScalar,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    private static void AssertBulkConversionsMatchScalar()
    {
        // Values immediately below, at, and above each vector width force every hardware regime through its vector body and scalar tail.
        int[] lengths = [0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 259];
        AssociatedAlphaPixelOperations<NormalizedByte4P> operations = (AssociatedAlphaPixelOperations<NormalizedByte4P>)PixelOperations<NormalizedByte4P>.Instance;

        foreach (int length in lengths)
        {
            NormalizedByte4P[] pixels = new NormalizedByte4P[length];
            Vector4[] expectedNative = new Vector4[length];
            Vector4[] expectedAssociatedNative = new Vector4[length];
            Vector4[] expectedAssociatedScaled = new Vector4[length];
            Vector4[] expectedUnassociatedNative = new Vector4[length];
            Vector4[] expectedUnassociatedScaled = new Vector4[length];
            Vector4[] actualNative = new Vector4[length];
            Vector4[] actualAssociatedNative = new Vector4[length];
            Vector4[] actualAssociatedScaled = new Vector4[length];
            Vector4[] actualUnassociatedNative = new Vector4[length];
            Vector4[] actualUnassociatedScaled = new Vector4[length];

            for (int i = 0; i < length; i++)
            {
                pixels[i].PackedValue = (uint)((byte)(i * 37 + 128) | ((byte)(i * 73 + 64) << 8) | ((byte)(i * 109 + 192) << 16) | ((byte)(i * 151 + 128) << 24));
                expectedNative[i] = pixels[i].ToVector4();
                expectedAssociatedNative[i] = pixels[i].ToAssociatedVector4();
                expectedAssociatedScaled[i] = pixels[i].ToAssociatedScaledVector4();
                expectedUnassociatedNative[i] = pixels[i].ToUnassociatedVector4();
                expectedUnassociatedScaled[i] = pixels[i].ToUnassociatedScaledVector4();
            }

            if (length > 0)
            {
                // PackedValue permits every signed byte pattern. Explicit -128 components ensure bulk sign extension matches scalar decoding even though the packer emits no -128 values.
                pixels[0].PackedValue = 0x80808080;
                expectedNative[0] = pixels[0].ToVector4();
                expectedAssociatedNative[0] = pixels[0].ToAssociatedVector4();
                expectedAssociatedScaled[0] = pixels[0].ToAssociatedScaledVector4();
                expectedUnassociatedNative[0] = pixels[0].ToUnassociatedVector4();
                expectedUnassociatedScaled[0] = pixels[0].ToUnassociatedScaledVector4();
            }

            operations.ToVector4(Configuration.Default, pixels, actualNative, PixelConversionModifiers.None);
            operations.ToVector4(Configuration.Default, pixels, actualAssociatedNative, PixelConversionModifiers.Premultiply);
            operations.ToVector4(Configuration.Default, pixels, actualAssociatedScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);
            operations.ToVector4(Configuration.Default, pixels, actualUnassociatedNative, PixelConversionModifiers.UnPremultiply);
            operations.ToVector4(Configuration.Default, pixels, actualUnassociatedScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

            Assert.Equal(expectedNative, actualNative);
            Assert.Equal(expectedAssociatedNative, actualAssociatedNative);
            Assert.Equal(expectedAssociatedScaled, actualAssociatedScaled);
            Assert.Equal(expectedUnassociatedNative, actualUnassociatedNative);
            Assert.Equal(expectedUnassociatedScaled, actualUnassociatedScaled);

            Vector4[] unassociatedScaled = new Vector4[length];
            Vector4[] associatedScaled = new Vector4[length];
            Vector4[] unassociatedNative = new Vector4[length];
            Vector4[] associatedNative = new Vector4[length];
            NormalizedByte4P[] expectedFromUnassociatedNative = new NormalizedByte4P[length];
            NormalizedByte4P[] expectedFromAssociatedNative = new NormalizedByte4P[length];
            NormalizedByte4P[] expectedFromUnassociatedScaled = new NormalizedByte4P[length];
            NormalizedByte4P[] expectedFromAssociatedScaled = new NormalizedByte4P[length];
            NormalizedByte4P[] actualFromUnassociatedNative = new NormalizedByte4P[length];
            NormalizedByte4P[] actualFromAssociatedNative = new NormalizedByte4P[length];
            NormalizedByte4P[] actualFromUnassociatedScaled = new NormalizedByte4P[length];
            NormalizedByte4P[] actualFromAssociatedScaled = new NormalizedByte4P[length];

            for (int i = 0; i < length; i++)
            {
                float alpha = ((i * 151) % 1021) / 1020F;
                unassociatedScaled[i] = new Vector4(((i * 37) % 1021) / 1020F, ((i * 73) % 1021) / 1020F, ((i * 109) % 1021) / 1020F, alpha);
                associatedScaled[i] = new Vector4(unassociatedScaled[i].X * alpha, unassociatedScaled[i].Y * alpha, unassociatedScaled[i].Z * alpha, alpha);

                // Signed-native formats encode the common scaled domain over [-1, 1], so native inputs must be derived before invoking the native entry points.
                unassociatedNative[i] = (unassociatedScaled[i] * 2F) - Vector4.One;
                associatedNative[i] = (associatedScaled[i] * 2F) - Vector4.One;
                expectedFromUnassociatedNative[i] = NormalizedByte4P.FromUnassociatedVector4(unassociatedNative[i]);
                expectedFromAssociatedNative[i] = NormalizedByte4P.FromAssociatedVector4(associatedNative[i]);
                expectedFromUnassociatedScaled[i] = NormalizedByte4P.FromUnassociatedScaledVector4(unassociatedScaled[i]);
                expectedFromAssociatedScaled[i] = NormalizedByte4P.FromAssociatedScaledVector4(associatedScaled[i]);
            }

            Vector4[] unassociatedNativeSource = [.. unassociatedNative];
            Vector4[] associatedNativeSource = [.. associatedNative];
            Vector4[] unassociatedScaledSource = [.. unassociatedScaled];
            Vector4[] associatedScaledSource = [.. associatedScaled];

            operations.FromVector4Destructive(Configuration.Default, unassociatedNativeSource, actualFromUnassociatedNative, PixelConversionModifiers.UnPremultiply);
            operations.FromVector4Destructive(Configuration.Default, associatedNativeSource, actualFromAssociatedNative, PixelConversionModifiers.Premultiply);
            operations.FromVector4Destructive(Configuration.Default, unassociatedScaledSource, actualFromUnassociatedScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
            operations.FromVector4Destructive(Configuration.Default, associatedScaledSource, actualFromAssociatedScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);

            Assert.Equal(expectedFromUnassociatedNative, actualFromUnassociatedNative);
            Assert.Equal(expectedFromAssociatedNative, actualFromAssociatedNative);
            Assert.Equal(expectedFromUnassociatedScaled, actualFromUnassociatedScaled);
            Assert.Equal(expectedFromAssociatedScaled, actualFromAssociatedScaled);

            NormalizedByte4P[] expectedFromNative = new NormalizedByte4P[length];
            NormalizedByte4P[] expectedFromScaled = new NormalizedByte4P[length];
            NormalizedByte4P[] actualFromNative = new NormalizedByte4P[length];
            NormalizedByte4P[] actualFromScaled = new NormalizedByte4P[length];

            for (int i = 0; i < length; i++)
            {
                expectedFromNative[i] = NormalizedByte4P.FromVector4(expectedNative[i]);
                expectedFromScaled[i] = NormalizedByte4P.FromScaledVector4(expectedAssociatedScaled[i]);
            }

            Vector4[] actualNativeSource = [.. expectedNative];
            Vector4[] actualScaledSource = [.. expectedAssociatedScaled];

            operations.FromVector4Destructive(Configuration.Default, actualNativeSource, actualFromNative, PixelConversionModifiers.None);
            operations.FromVector4Destructive(Configuration.Default, actualScaledSource, actualFromScaled, PixelConversionModifiers.Scale);

            Assert.Equal(expectedFromNative, actualFromNative);
            Assert.Equal(expectedFromScaled, actualFromScaled);
        }
    }
}

/// <summary>
/// Tests the <see cref="HalfVector4P"/> pixel format.
/// </summary>
public class HalfVector4PTests : AssociatedAlphaPixelTests<HalfVector4P>
{
    [Fact]
    public void PackedValueMatchesHalfVector4ForAssociatedVector()
    {
        Vector4 associated = new(.1F, -.3F, .5F, -.7F);

        Assert.Equal(new HalfVector4(associated).PackedValue, new HalfVector4P(associated).PackedValue);
    }

    [Fact]
    public void ColorRoundTripDoesNotIntroduceAdditionalAssociationLoss()
    {
        HalfVector4P source = HalfVector4P.FromAssociatedScaledVector4(new Vector4(.125F, .25F, .375F, .5F));
        HalfVector4P expected = HalfVector4P.FromScaledVector4(source.ToScaledVector4());
        Color color = Color.FromPixel(source);
        Color[] bulkColors = new Color[1];

        Color.FromPixel<HalfVector4P>(new[] { source }, bulkColors);

        Assert.Equal(source.ToScaledVector4(), color.ToScaledVector4());
        Assert.Equal(expected, color.ToPixel<HalfVector4P>());
        Assert.Equal(source.ToScaledVector4(), bulkColors[0].ToScaledVector4());
        Assert.Equal(expected, bulkColors[0].ToPixel<HalfVector4P>());
    }

    [Fact]
    public void BulkConversionsMatchScalarAcrossHardwareWidths() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertBulkConversionsMatchScalar,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    private static void AssertBulkConversionsMatchScalar()
    {
        AssertEveryHalfBitPatternUnpacksExactly();
        AssertHalfPackingMatchesScalar();
        AssertAlphaConversionsMatchScalar();
    }

    private static void AssertEveryHalfBitPatternUnpacksExactly()
    {
        HalfVector4P[] pixels = new HalfVector4P[(ushort.MaxValue + 1) / 4];
        Vector4[] expectedNative = new Vector4[pixels.Length];
        Vector4[] expectedScaled = new Vector4[pixels.Length];
        Vector4[] actualNative = new Vector4[pixels.Length];
        Vector4[] actualScaled = new Vector4[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            ulong x = (ushort)(i * 4);
            ulong y = (ushort)((i * 4) + 1);
            ulong z = (ushort)((i * 4) + 2);
            ulong w = (ushort)((i * 4) + 3);
            pixels[i].PackedValue = x | (y << 16) | (z << 32) | (w << 48);
            expectedNative[i] = pixels[i].ToVector4();
            expectedScaled[i] = pixels[i].ToScaledVector4();
        }

        PixelOperations<HalfVector4P>.Instance.ToVector4(Configuration.Default, pixels, actualNative, PixelConversionModifiers.None);
        PixelOperations<HalfVector4P>.Instance.ToVector4(Configuration.Default, pixels, actualScaled, PixelConversionModifiers.Scale);

        AssertVectorBitsEqual(expectedNative, actualNative);
        AssertVectorBitsEqual(expectedScaled, actualScaled);
    }

    private static void AssertHalfPackingMatchesScalar()
    {
        const int finitePositiveHalfCount = 0x7BFF;
        const int distributedFloatCount = ushort.MaxValue + 1;
        const int componentCount = (ushort.MaxValue + 1) + (finitePositiveHalfCount * 2) + distributedFloatCount + 2;
        Vector4[] source = new Vector4[componentCount / 4];
        Span<float> components = MemoryMarshal.Cast<Vector4, float>(source.AsSpan());
        int index = 0;

        for (int bits = 0; bits <= ushort.MaxValue; bits++)
        {
            components[index++] = (float)BitConverter.UInt16BitsToHalf((ushort)bits);
        }

        for (int bits = 0; bits < finitePositiveHalfCount; bits++)
        {
            float lower = (float)BitConverter.UInt16BitsToHalf((ushort)bits);
            float upper = (float)BitConverter.UInt16BitsToHalf((ushort)(bits + 1));

            // Every midpoint exercises binary16 round-to-nearest-even; negating it covers the symmetric sign path.
            float midpoint = (lower + upper) * .5F;
            components[index++] = midpoint;
            components[index++] = -midpoint;
        }

        // Equal high and low words distribute samples across the complete binary32 sign, exponent, and fraction space.
        for (uint bits = 0; bits <= ushort.MaxValue; bits++)
        {
            components[index++] = BitConverter.UInt32BitsToSingle(bits * 0x0001_0001U);
        }

        // The two padding components also verify finite overflow in both directions.
        components[index++] = float.MaxValue;
        components[index] = float.MinValue;

        HalfVector4P[] expected = new HalfVector4P[source.Length];
        HalfVector4P[] actual = new HalfVector4P[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            expected[i] = HalfVector4P.FromVector4(source[i]);
        }

        Vector4[] destructiveSource = [.. source];

        PixelOperations<HalfVector4P>.Instance.FromVector4Destructive(Configuration.Default, destructiveSource, actual, PixelConversionModifiers.None);

        for (int i = 0; i < expected.Length; i++)
        {
            for (int component = 0; component < 4; component++)
            {
                int shift = component * 16;
                ushort expectedBits = (ushort)(expected[i].PackedValue >> shift);
                ushort actualBits = (ushort)(actual[i].PackedValue >> shift);

                // Association performs floating-point arithmetic before packing. IEEE 754 requires a NaN result but does not specify which operand's payload survives that arithmetic.
                if (Half.IsNaN(BitConverter.UInt16BitsToHalf(expectedBits)))
                {
                    Assert.True(Half.IsNaN(BitConverter.UInt16BitsToHalf(actualBits)), $"Pixel {i}, component {component} should be NaN.");
                }
                else
                {
                    Assert.Equal(expectedBits, actualBits);
                }
            }
        }
    }

    private static void AssertAlphaConversionsMatchScalar()
    {
        // Values immediately below, at, and above each vector width force every hardware regime through its vector body and scalar tail.
        int[] lengths = [0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 259];
        AssociatedAlphaPixelOperations<HalfVector4P> operations = (AssociatedAlphaPixelOperations<HalfVector4P>)PixelOperations<HalfVector4P>.Instance;

        foreach (int length in lengths)
        {
            Vector4[] unassociatedScaled = new Vector4[length];
            Vector4[] associatedScaled = new Vector4[length];
            Vector4[] unassociatedNative = new Vector4[length];
            Vector4[] associatedNative = new Vector4[length];
            HalfVector4P[] expectedFromUnassociatedNative = new HalfVector4P[length];
            HalfVector4P[] expectedFromAssociatedNative = new HalfVector4P[length];
            HalfVector4P[] expectedFromUnassociatedScaled = new HalfVector4P[length];
            HalfVector4P[] expectedFromAssociatedScaled = new HalfVector4P[length];
            HalfVector4P[] actualFromUnassociatedNative = new HalfVector4P[length];
            HalfVector4P[] actualFromAssociatedNative = new HalfVector4P[length];
            HalfVector4P[] actualFromUnassociatedScaled = new HalfVector4P[length];
            HalfVector4P[] actualFromAssociatedScaled = new HalfVector4P[length];

            for (int i = 0; i < length; i++)
            {
                float alpha = ((i * 151) % 4093) / 4092F;
                unassociatedScaled[i] = new Vector4(((i * 37) % 4093) / 4092F, ((i * 73) % 4093) / 4092F, ((i * 109) % 4093) / 4092F, alpha);
                associatedScaled[i] = new Vector4(unassociatedScaled[i].X * alpha, unassociatedScaled[i].Y * alpha, unassociatedScaled[i].Z * alpha, alpha);

                unassociatedNative[i] = (unassociatedScaled[i] * 131008F) - new Vector4(65504F);
                associatedNative[i] = (associatedScaled[i] * 131008F) - new Vector4(65504F);
                expectedFromUnassociatedNative[i] = HalfVector4P.FromUnassociatedVector4(unassociatedNative[i]);
                expectedFromAssociatedNative[i] = HalfVector4P.FromAssociatedVector4(associatedNative[i]);
                expectedFromUnassociatedScaled[i] = HalfVector4P.FromUnassociatedScaledVector4(unassociatedScaled[i]);
                expectedFromAssociatedScaled[i] = HalfVector4P.FromAssociatedScaledVector4(associatedScaled[i]);
            }

            Vector4[] unassociatedNativeSource = [.. unassociatedNative];
            Vector4[] associatedNativeSource = [.. associatedNative];
            Vector4[] unassociatedScaledSource = [.. unassociatedScaled];
            Vector4[] associatedScaledSource = [.. associatedScaled];

            operations.FromVector4Destructive(Configuration.Default, unassociatedNativeSource, actualFromUnassociatedNative, PixelConversionModifiers.UnPremultiply);
            operations.FromVector4Destructive(Configuration.Default, associatedNativeSource, actualFromAssociatedNative, PixelConversionModifiers.Premultiply);
            operations.FromVector4Destructive(Configuration.Default, unassociatedScaledSource, actualFromUnassociatedScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
            operations.FromVector4Destructive(Configuration.Default, associatedScaledSource, actualFromAssociatedScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);

            Assert.Equal(expectedFromUnassociatedNative, actualFromUnassociatedNative);
            Assert.Equal(expectedFromAssociatedNative, actualFromAssociatedNative);
            Assert.Equal(expectedFromUnassociatedScaled, actualFromUnassociatedScaled);
            Assert.Equal(expectedFromAssociatedScaled, actualFromAssociatedScaled);

            Vector4[] expectedAssociatedNative = new Vector4[length];
            Vector4[] expectedAssociatedScaled = new Vector4[length];
            Vector4[] expectedUnassociatedNative = new Vector4[length];
            Vector4[] expectedUnassociatedScaled = new Vector4[length];
            Vector4[] actualAssociatedNative = new Vector4[length];
            Vector4[] actualAssociatedScaled = new Vector4[length];
            Vector4[] actualUnassociatedNative = new Vector4[length];
            Vector4[] actualUnassociatedScaled = new Vector4[length];

            for (int i = 0; i < length; i++)
            {
                expectedAssociatedNative[i] = expectedFromAssociatedScaled[i].ToAssociatedVector4();
                expectedAssociatedScaled[i] = expectedFromAssociatedScaled[i].ToAssociatedScaledVector4();
                expectedUnassociatedNative[i] = expectedFromAssociatedScaled[i].ToUnassociatedVector4();
                expectedUnassociatedScaled[i] = expectedFromAssociatedScaled[i].ToUnassociatedScaledVector4();
            }

            operations.ToVector4(Configuration.Default, expectedFromAssociatedScaled, actualAssociatedNative, PixelConversionModifiers.Premultiply);
            operations.ToVector4(Configuration.Default, expectedFromAssociatedScaled, actualAssociatedScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);
            operations.ToVector4(Configuration.Default, expectedFromAssociatedScaled, actualUnassociatedNative, PixelConversionModifiers.UnPremultiply);
            operations.ToVector4(Configuration.Default, expectedFromAssociatedScaled, actualUnassociatedScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

            AssertVectorBitsEqual(expectedAssociatedNative, actualAssociatedNative);
            AssertVectorBitsEqual(expectedAssociatedScaled, actualAssociatedScaled);
            AssertVectorBitsEqual(expectedUnassociatedNative, actualUnassociatedNative);
            AssertVectorBitsEqual(expectedUnassociatedScaled, actualUnassociatedScaled);
        }
    }

    private static void AssertVectorBitsEqual(ReadOnlySpan<Vector4> expected, ReadOnlySpan<Vector4> actual)
    {
        ReadOnlySpan<uint> expectedBits = MemoryMarshal.Cast<Vector4, uint>(expected);
        ReadOnlySpan<uint> actualBits = MemoryMarshal.Cast<Vector4, uint>(actual);

        Assert.Equal(expectedBits.ToArray(), actualBits.ToArray());
    }
}

/// <summary>
/// Tests conversion between associated-alpha packed byte layouts.
/// </summary>
public class AssociatedAlphaPackedPixelConversionTests
{
    [Fact]
    public void Rgba32PToBgra32PRoundTripIsLossless() => AssertLosslessRoundTrip<Bgra32P>();

    [Fact]
    public void Rgba32PToArgb32PRoundTripIsLossless() => AssertLosslessRoundTrip<Argb32P>();

    [Fact]
    public void Rgba32PToAbgr32PRoundTripIsLossless() => AssertLosslessRoundTrip<Abgr32P>();

    [Fact]
    public void Rgba32PScalarAndBulkAssociatedVectorsAreEqual() => AssertScalarAndBulkAssociatedVectorsAreEqual((r, g, b, a) => new Rgba32P(r, g, b, a));

    [Fact]
    public void Bgra32PScalarAndBulkAssociatedVectorsAreEqual() => AssertScalarAndBulkAssociatedVectorsAreEqual((r, g, b, a) => new Bgra32P(r, g, b, a));

    [Fact]
    public void Argb32PScalarAndBulkAssociatedVectorsAreEqual() => AssertScalarAndBulkAssociatedVectorsAreEqual((r, g, b, a) => new Argb32P(r, g, b, a));

    [Fact]
    public void Abgr32PScalarAndBulkAssociatedVectorsAreEqual() => AssertScalarAndBulkAssociatedVectorsAreEqual((r, g, b, a) => new Abgr32P(r, g, b, a));

    [Fact]
    public void Rgba32PScalarAndBulkFromAssociatedVectorsAreEqual() => AssertScalarAndBulkFromAssociatedVectorsAreEqual<Rgba32P>();

    [Fact]
    public void Bgra32PScalarAndBulkFromAssociatedVectorsAreEqual() => AssertScalarAndBulkFromAssociatedVectorsAreEqual<Bgra32P>();

    [Fact]
    public void Argb32PScalarAndBulkFromAssociatedVectorsAreEqual() => AssertScalarAndBulkFromAssociatedVectorsAreEqual<Argb32P>();

    [Fact]
    public void Abgr32PScalarAndBulkFromAssociatedVectorsAreEqual() => AssertScalarAndBulkFromAssociatedVectorsAreEqual<Abgr32P>();

    [Fact]
    public void Rgba32PScalarAndBulkUnassociatedVectorsAreEqual() => AssertScalarAndBulkUnassociatedVectorsAreEqual((r, g, b, a) => new Rgba32P(r, g, b, a));

    [Fact]
    public void Bgra32PScalarAndBulkUnassociatedVectorsAreEqual() => AssertScalarAndBulkUnassociatedVectorsAreEqual((r, g, b, a) => new Bgra32P(r, g, b, a));

    [Fact]
    public void Argb32PScalarAndBulkUnassociatedVectorsAreEqual() => AssertScalarAndBulkUnassociatedVectorsAreEqual((r, g, b, a) => new Argb32P(r, g, b, a));

    [Fact]
    public void Abgr32PScalarAndBulkUnassociatedVectorsAreEqual() => AssertScalarAndBulkUnassociatedVectorsAreEqual((r, g, b, a) => new Abgr32P(r, g, b, a));

    [Fact]
    public void Rgba32PScalarAndBulkFromUnassociatedVectorsAreEqual() => AssertScalarAndBulkFromUnassociatedVectorsAreEqual<Rgba32P>();

    [Fact]
    public void Bgra32PScalarAndBulkFromUnassociatedVectorsAreEqual() => AssertScalarAndBulkFromUnassociatedVectorsAreEqual<Bgra32P>();

    [Fact]
    public void Argb32PScalarAndBulkFromUnassociatedVectorsAreEqual() => AssertScalarAndBulkFromUnassociatedVectorsAreEqual<Argb32P>();

    [Fact]
    public void Abgr32PScalarAndBulkFromUnassociatedVectorsAreEqual() => AssertScalarAndBulkFromUnassociatedVectorsAreEqual<Abgr32P>();

    [Fact]
    public void BulkConversionsMatchScalarAcrossHardwareWidths()
    {
        // RemoteExecutor resolves the delegate by method name alone, so its entry point must not share the generic worker's name.
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertPackedBulkConversionsMatchScalar,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    }

    private static void AssertPackedBulkConversionsMatchScalar()
    {
        AssertBulkConversionsMatchScalar((r, g, b, a) => new Rgba32P(r, g, b, a));
        AssertBulkConversionsMatchScalar((r, g, b, a) => new Bgra32P(r, g, b, a));
        AssertBulkConversionsMatchScalar((r, g, b, a) => new Argb32P(r, g, b, a));
        AssertBulkConversionsMatchScalar((r, g, b, a) => new Abgr32P(r, g, b, a));
    }

    private static void AssertBulkConversionsMatchScalar<TPixel>(Func<byte, byte, byte, byte, TPixel> createPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        AssertScalarAndBulkAssociatedVectorsAreEqual(createPixel);
        AssertScalarAndBulkFromAssociatedVectorsAreEqual<TPixel>();
        AssertScalarAndBulkUnassociatedVectorsAreEqual(createPixel);
        AssertScalarAndBulkFromUnassociatedVectorsAreEqual<TPixel>();
    }

    private static void AssertLosslessRoundTrip<TIntermediate>()
        where TIntermediate : unmanaged, IPixel<TIntermediate>
    {
        const int componentAlphaPairCount = ((byte.MaxValue + 1) * (byte.MaxValue + 2)) / 2;
        const int colorChannelCount = 3;
        Rgba32P[] expected = new Rgba32P[componentAlphaPairCount * colorChannelCount];
        TIntermediate[] intermediate = new TIntermediate[expected.Length];
        Rgba32P[] actual = new Rgba32P[expected.Length];
        int index = 0;

        // Every component/alpha pair in the valid associated-byte domain must survive a layout
        // conversion exactly. Exercising each color lane also catches an incorrect channel map.
        for (int alpha = 0; alpha <= byte.MaxValue; alpha++)
        {
            for (int component = 0; component <= alpha; component++)
            {
                expected[index++] = new Rgba32P((byte)component, 0, 0, (byte)alpha);
                expected[index++] = new Rgba32P(0, (byte)component, 0, (byte)alpha);
                expected[index++] = new Rgba32P(0, 0, (byte)component, (byte)alpha);
            }
        }

        PixelOperations<TIntermediate>.Instance.From<Rgba32P>(Configuration.Default, expected, intermediate);
        PixelOperations<Rgba32P>.Instance.From<TIntermediate>(Configuration.Default, intermediate, actual);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].ToScaledVector4(), intermediate[i].ToScaledVector4());
        }

        Assert.Equal(expected, actual);
    }

    private static void AssertScalarAndBulkAssociatedVectorsAreEqual<TPixel>(Func<byte, byte, byte, byte, TPixel> createPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Prefixes straddle every vector width; the final length also preserves exhaustive byte-component coverage and adds a scalar tail.
        int[] lengths = [0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 259, byte.MaxValue * 4 + 5];
        TPixel[] source = new TPixel[lengths[^1]];
        AssociatedAlphaPixelOperations<TPixel> operations = (AssociatedAlphaPixelOperations<TPixel>)PixelOperations<TPixel>.Instance;

        for (int component = 0; component <= byte.MaxValue; component++)
        {
            int index = component * 4;
            source[index] = createPixel((byte)component, 0, 0, byte.MaxValue);
            source[index + 1] = createPixel(0, (byte)component, 0, byte.MaxValue);
            source[index + 2] = createPixel(0, 0, (byte)component, byte.MaxValue);
            source[index + 3] = createPixel(0, 0, 0, (byte)component);
        }

        source[^1] = createPixel(37, 73, 109, 151);

        foreach (int length in lengths)
        {
            ReadOnlySpan<TPixel> pixels = source.AsSpan(0, length);
            Vector4[] expectedNative = new Vector4[length];
            Vector4[] expectedScaled = new Vector4[length];
            Vector4[] actualNative = new Vector4[length];
            Vector4[] actualScaled = new Vector4[length];

            for (int i = 0; i < pixels.Length; i++)
            {
                expectedNative[i] = pixels[i].ToAssociatedVector4();
                expectedScaled[i] = pixels[i].ToAssociatedScaledVector4();
            }

            operations.ToVector4(Configuration.Default, pixels, actualNative, PixelConversionModifiers.Premultiply);
            operations.ToVector4(Configuration.Default, pixels, actualScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);

            Assert.Equal(expectedNative, actualNative);
            Assert.Equal(expectedScaled, actualScaled);
        }
    }

    private static void AssertScalarAndBulkFromAssociatedVectorsAreEqual<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Values immediately below, at, and above each vector width force every hardware regime through its vector body and scalar tail.
        int[] lengths = [0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 259];
        Vector4[] source = new Vector4[lengths[^1]];
        AssociatedAlphaPixelOperations<TPixel> operations = (AssociatedAlphaPixelOperations<TPixel>)PixelOperations<TPixel>.Instance;

        for (int i = 0; i < source.Length; i++)
        {
            // Alternating exact byte alpha values with fractional values covers both reassociation branches.
            float alpha = (i & 1) == 0 ? (i % 256) / 255F : ((i % 255) + .375F) / 255F;
            source[i] = new Vector4(((i * 37) % 256) / 255F, ((i * 73) % 256) / 255F, ((i * 109) % 256) / 255F, 1F) * alpha;
            source[i].W = alpha;
        }

        foreach (int length in lengths)
        {
            Vector4[] nativeSource = source.AsSpan(0, length).ToArray();
            Vector4[] scaledSource = source.AsSpan(0, length).ToArray();
            TPixel[] expectedNative = new TPixel[length];
            TPixel[] expectedScaled = new TPixel[length];
            TPixel[] actualNative = new TPixel[length];
            TPixel[] actualScaled = new TPixel[length];

            for (int i = 0; i < length; i++)
            {
                expectedNative[i] = TPixel.FromAssociatedVector4(nativeSource[i]);
                expectedScaled[i] = TPixel.FromAssociatedScaledVector4(scaledSource[i]);
            }

            operations.FromVector4Destructive(Configuration.Default, nativeSource, actualNative, PixelConversionModifiers.Premultiply);
            operations.FromVector4Destructive(Configuration.Default, scaledSource, actualScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);

            Assert.Equal(expectedNative, actualNative);
            Assert.Equal(expectedScaled, actualScaled);
        }
    }

    private static void AssertScalarAndBulkUnassociatedVectorsAreEqual<TPixel>(Func<byte, byte, byte, byte, TPixel> createPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int[] lengths = [0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 259];
        AssociatedAlphaPixelOperations<TPixel> operations = (AssociatedAlphaPixelOperations<TPixel>)PixelOperations<TPixel>.Instance;

        foreach (int length in lengths)
        {
            TPixel[] pixels = new TPixel[length];
            Vector4[] expectedNative = new Vector4[length];
            Vector4[] expectedScaled = new Vector4[length];
            Vector4[] actualNative = new Vector4[length];
            Vector4[] actualScaled = new Vector4[length];

            for (int i = 0; i < pixels.Length; i++)
            {
                // Independent component sequences exercise every packed layout across each SIMD width and remainder.
                pixels[i] = createPixel((byte)((i * 37) % 256), (byte)((i * 73) % 256), (byte)((i * 109) % 256), (byte)((i * 151) % 256));
                expectedNative[i] = pixels[i].ToUnassociatedVector4();
                expectedScaled[i] = pixels[i].ToUnassociatedScaledVector4();
            }

            if (length > 0)
            {
                // A zero-alpha pixel with stored RGB verifies the converter's defined zero-divisor behavior outside the lossless straight-color round-trip domain.
                pixels[0] = createPixel(37, 73, 109, 0);
                expectedNative[0] = pixels[0].ToUnassociatedVector4();
                expectedScaled[0] = pixels[0].ToUnassociatedScaledVector4();
            }

            operations.ToVector4(Configuration.Default, pixels, actualNative, PixelConversionModifiers.UnPremultiply);
            operations.ToVector4(Configuration.Default, pixels, actualScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

            Assert.Equal(expectedNative, actualNative);
            Assert.Equal(expectedScaled, actualScaled);
        }
    }

    private static void AssertScalarAndBulkFromUnassociatedVectorsAreEqual<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int[] lengths = [0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 259];
        AssociatedAlphaPixelOperations<TPixel> operations = (AssociatedAlphaPixelOperations<TPixel>)PixelOperations<TPixel>.Instance;

        foreach (int length in lengths)
        {
            Vector4[] vectors = new Vector4[length];
            TPixel[] expectedNative = new TPixel[length];
            TPixel[] expectedScaled = new TPixel[length];
            TPixel[] actualNative = new TPixel[length];
            TPixel[] actualScaled = new TPixel[length];

            for (int i = 0; i < vectors.Length; i++)
            {
                // Fractional alpha values verify destination-alpha quantization across each SIMD width and remainder.
                vectors[i] = new Vector4(((i * 37) % 1021) / 1020F, ((i * 73) % 1021) / 1020F, ((i * 109) % 1021) / 1020F, ((i * 151) % 1021) / 1020F);
                expectedNative[i] = TPixel.FromUnassociatedVector4(vectors[i]);
                expectedScaled[i] = TPixel.FromUnassociatedScaledVector4(vectors[i]);
            }

            Vector4[] nativeSource = [.. vectors];
            Vector4[] scaledSource = [.. vectors];

            operations.FromVector4Destructive(Configuration.Default, nativeSource, actualNative, PixelConversionModifiers.UnPremultiply);
            operations.FromVector4Destructive(Configuration.Default, scaledSource, actualScaled, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

            Assert.Equal(expectedNative, actualNative);
            Assert.Equal(expectedScaled, actualScaled);
        }
    }
}

/// <summary>
/// Tests conversion between associated and unassociated packed byte formats.
/// </summary>
public class AssociatedToUnassociatedPackedPixelConversionTests
{
    [Fact]
    public void Rgba32ToRgba32PMatchesExactAssociationForEveryComponentAndAlpha()
        => AssertUnsignedByteAssociation((red, green, blue, alpha) => new Rgba32P(red, green, blue, alpha));

    [Fact]
    public void Rgba32ToBgra32PMatchesExactAssociationForEveryComponentAndAlpha()
        => AssertUnsignedByteAssociation((red, green, blue, alpha) => new Bgra32P(red, green, blue, alpha));

    [Fact]
    public void Rgba32ToArgb32PMatchesExactAssociationForEveryComponentAndAlpha()
        => AssertUnsignedByteAssociation((red, green, blue, alpha) => new Argb32P(red, green, blue, alpha));

    [Fact]
    public void Rgba32ToAbgr32PMatchesExactAssociationForEveryComponentAndAlpha()
        => AssertUnsignedByteAssociation((red, green, blue, alpha) => new Abgr32P(red, green, blue, alpha));

    [Fact]
    public void Rgba32ToNormalizedByte4PMatchesExactAssociationForEveryComponentAndAlpha()
    {
        const int pairCount = 65536;
        const int channelCount = 3;
        Rgba32[] source = new Rgba32[pairCount * channelCount];
        NormalizedByte4P[] expected = new NormalizedByte4P[source.Length];
        NormalizedByte4P[] actualBulk = new NormalizedByte4P[source.Length];
        int index = 0;

        for (int alpha = 0; alpha <= byte.MaxValue; alpha++)
        {
            // NormalizedByte4 has 254 intervals from -1 through 1, so alpha is first rounded to that destination grid.
            int destinationAlpha = ((alpha * 254) + 127) / byte.MaxValue;

            for (int unassociated = 0; unassociated <= byte.MaxValue; unassociated++)
            {
                // Association uses the alpha representable by the destination, then rounds the result to the same 254-interval grid.
                int associated = ((unassociated * destinationAlpha) + 127) / byte.MaxValue;

                source[index] = new Rgba32((byte)unassociated, 0, 0, (byte)alpha);
                expected[index++] = CreateNormalizedByte4P(associated, 0, 0, destinationAlpha);
                source[index] = new Rgba32(0, (byte)unassociated, 0, (byte)alpha);
                expected[index++] = CreateNormalizedByte4P(0, associated, 0, destinationAlpha);
                source[index] = new Rgba32(0, 0, (byte)unassociated, (byte)alpha);
                expected[index++] = CreateNormalizedByte4P(0, 0, associated, destinationAlpha);
            }
        }

        PixelOperations<NormalizedByte4P>.Instance.From<Rgba32>(Configuration.Default, source, actualBulk);

        Assert.Equal(expected, actualBulk);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(expected[i], NormalizedByte4P.FromRgba32(source[i]));
            Assert.Equal(expected[i], Color.FromPixel(source[i]).ToPixel<NormalizedByte4P>());
        }
    }

    [Fact]
    public void Rgba32ToHalfVector4PMatchesExactAssociationForEveryComponentAndAlpha()
    {
        const int pairCount = 65536;
        const int channelCount = 3;

        const float finiteMinimum = -65504F;
        const float finiteRange = 131008F;
        const float inverseFiniteRange = (float)(1D / finiteRange);
        const ulong zeroComponentBits = 0xFBFF;
        Rgba32[] source = new Rgba32[pairCount * channelCount];
        HalfVector4P[] expected = new HalfVector4P[source.Length];
        HalfVector4P[] actualBulk = new HalfVector4P[source.Length];
        int index = 0;

        for (int alpha = 0; alpha <= byte.MaxValue; alpha++)
        {
            // Association must use the alpha value recovered from the destination half, rather than the higher-precision source alpha.
            float normalizedAlpha = (float)(alpha / (double)byte.MaxValue);
            ushort alphaBits = BitConverter.HalfToUInt16Bits((Half)((normalizedAlpha * finiteRange) + finiteMinimum));
            float destinationAlpha = ((float)BitConverter.UInt16BitsToHalf(alphaBits) * inverseFiniteRange) + .5F;

            for (int unassociated = 0; unassociated <= byte.MaxValue; unassociated++)
            {
                float normalizedComponent = (float)(unassociated / (double)byte.MaxValue);
                float associated = normalizedComponent * destinationAlpha;
                ushort associatedBits = BitConverter.HalfToUInt16Bits((Half)((associated * finiteRange) + finiteMinimum));
                ulong alphaPacked = (ulong)alphaBits << 48;

                source[index] = new Rgba32((byte)unassociated, 0, 0, (byte)alpha);
                expected[index++] = new HalfVector4P { PackedValue = associatedBits | (zeroComponentBits << 16) | (zeroComponentBits << 32) | alphaPacked };
                source[index] = new Rgba32(0, (byte)unassociated, 0, (byte)alpha);
                expected[index++] = new HalfVector4P { PackedValue = zeroComponentBits | ((ulong)associatedBits << 16) | (zeroComponentBits << 32) | alphaPacked };
                source[index] = new Rgba32(0, 0, (byte)unassociated, (byte)alpha);
                expected[index++] = new HalfVector4P { PackedValue = zeroComponentBits | (zeroComponentBits << 16) | ((ulong)associatedBits << 32) | alphaPacked };
            }
        }

        PixelOperations<HalfVector4P>.Instance.From<Rgba32>(Configuration.Default, source, actualBulk);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(expected[i], HalfVector4P.FromRgba32(source[i]));
            Assert.Equal(expected[i], Color.FromPixel(source[i]).ToPixel<HalfVector4P>());
            Assert.Equal(expected[i], actualBulk[i]);
        }
    }

    [Fact]
    public void Rgba32PToRgba32ScalarRoundTripPreservesEveryLosslesslyRoundTrippableAssociatedComponent()
    {
        for (int alpha = 0; alpha <= byte.MaxValue; alpha++)
        {
            // Components greater than alpha are valid for additive blending but cannot round-trip losslessly through an 8-bit unassociated pixel.
            // The exhaustive lossless domain is therefore triangular rather than 256 squared.
            for (int associated = 0; associated <= alpha; associated++)
            {
                // This integer expression is the exact nearest 8-bit unassociated value for associated * 255 / alpha.
                byte unassociated = alpha == 0 ? (byte)0 : (byte)(((associated * byte.MaxValue) + (alpha / 2)) / alpha);
                Rgba32P source = new((byte)associated, 0, 0, (byte)alpha);
                Rgba32 expectedUnassociated = new(unassociated, 0, 0, (byte)alpha);

                Rgba32 actualUnassociated = source.ToRgba32();
                Rgba32P actualRoundTrip = Rgba32P.FromRgba32(actualUnassociated);

                Assert.Equal(expectedUnassociated, actualUnassociated);
                Assert.Equal(source, actualRoundTrip);
            }
        }
    }

    [Fact]
    public void ColorFromRgba32PPreservesEveryLosslesslyRoundTrippableAssociatedComponent()
    {
        const int pairCount = 32896;
        Rgba32P[] sourcePixels = new Rgba32P[pairCount];
        Color[] bulkColors = new Color[pairCount];
        int index = 0;

        for (int alpha = 0; alpha <= byte.MaxValue; alpha++)
        {
            for (int associated = 0; associated <= alpha; associated++)
            {
                byte unassociated = alpha == 0 ? (byte)0 : (byte)(((associated * byte.MaxValue) + (alpha / 2)) / alpha);
                Rgba32P source = new((byte)associated, 0, 0, (byte)alpha);
                Color color = Color.FromPixel(source);
                sourcePixels[index++] = source;

                Assert.Equal(new Rgba32(unassociated, 0, 0, (byte)alpha), color.ToPixel<Rgba32>());
                Assert.Equal(source, color.ToPixel<Rgba32P>());
            }
        }

        Color.FromPixel<Rgba32P>(sourcePixels, bulkColors);

        for (int i = 0; i < sourcePixels.Length; i++)
        {
            Assert.Equal(sourcePixels[i], bulkColors[i].ToPixel<Rgba32P>());
        }
    }

    [Fact]
    public void Rgba32PToBgra32RoundTripPreservesEveryLosslesslyRoundTrippableAssociatedComponent()
        => AssertUnsignedByteBulkRoundTrip((red, green, blue, alpha) => new Rgba32P(red, green, blue, alpha));

    [Fact]
    public void Bgra32PToBgra32RoundTripPreservesEveryLosslesslyRoundTrippableAssociatedComponent()
        => AssertUnsignedByteBulkRoundTrip((red, green, blue, alpha) => new Bgra32P(red, green, blue, alpha));

    [Fact]
    public void Argb32PToBgra32RoundTripPreservesEveryLosslesslyRoundTrippableAssociatedComponent()
        => AssertUnsignedByteBulkRoundTrip((red, green, blue, alpha) => new Argb32P(red, green, blue, alpha));

    [Fact]
    public void Abgr32PToBgra32RoundTripPreservesEveryLosslesslyRoundTrippableAssociatedComponent()
        => AssertUnsignedByteBulkRoundTrip((red, green, blue, alpha) => new Abgr32P(red, green, blue, alpha));

    [Fact]
    public void NormalizedByte4PToRgba32ScalarRoundTripPreservesEveryLosslesslyRoundTrippableAssociatedComponent()
    {
        for (int alpha = 0; alpha < byte.MaxValue; alpha++)
        {
            for (int associated = 0; associated <= alpha; associated++)
            {
                byte unassociated = alpha == 0 ? (byte)0 : (byte)(((associated * byte.MaxValue) + (alpha / 2)) / alpha);
                byte unassociatedAlpha = (byte)(((alpha * byte.MaxValue) + 127) / 254);
                NormalizedByte4P source = CreateNormalizedByte4P(associated, 0, 0, alpha);

                Rgba32 actualUnassociated = source.ToRgba32();
                NormalizedByte4P actualRoundTrip = NormalizedByte4P.FromRgba32(actualUnassociated);

                Assert.Equal(new Rgba32(unassociated, 0, 0, unassociatedAlpha), actualUnassociated);
                Assert.Equal(source, actualRoundTrip);
            }
        }
    }

    [Fact]
    public void ColorFromNormalizedByte4PPreservesEveryLosslesslyRoundTrippableAssociatedComponent()
    {
        const int pairCount = 32640;
        NormalizedByte4P[] sourcePixels = new NormalizedByte4P[pairCount];
        Color[] bulkColors = new Color[pairCount];
        int index = 0;

        for (int alpha = 0; alpha < byte.MaxValue; alpha++)
        {
            for (int associated = 0; associated <= alpha; associated++)
            {
                byte unassociated = alpha == 0 ? (byte)0 : (byte)(((associated * byte.MaxValue) + (alpha / 2)) / alpha);
                byte unassociatedAlpha = (byte)(((alpha * byte.MaxValue) + 127) / 254);
                NormalizedByte4P source = CreateNormalizedByte4P(associated, 0, 0, alpha);
                Color color = Color.FromPixel(source);
                sourcePixels[index++] = source;

                Assert.Equal(new Rgba32(unassociated, 0, 0, unassociatedAlpha), color.ToPixel<Rgba32>());
                Assert.Equal(source, color.ToPixel<NormalizedByte4P>());
            }
        }

        Color.FromPixel<NormalizedByte4P>(sourcePixels, bulkColors);

        for (int i = 0; i < sourcePixels.Length; i++)
        {
            Assert.Equal(sourcePixels[i], bulkColors[i].ToPixel<NormalizedByte4P>());
        }
    }

    [Fact]
    public void NormalizedByte4PToBgra32RoundTripPreservesEveryLosslesslyRoundTrippableAssociatedComponent()
    {
        const int pairCount = 32640;
        const int channelCount = 3;
        NormalizedByte4P[] source = new NormalizedByte4P[pairCount * channelCount];
        Bgra32[] expectedUnassociated = new Bgra32[source.Length];
        Bgra32[] actualUnassociated = new Bgra32[source.Length];
        NormalizedByte4P[] actualRoundTrip = new NormalizedByte4P[source.Length];
        int index = 0;

        for (int alpha = 0; alpha < byte.MaxValue; alpha++)
        {
            for (int associated = 0; associated <= alpha; associated++)
            {
                byte unassociated = alpha == 0 ? (byte)0 : (byte)(((associated * byte.MaxValue) + (alpha / 2)) / alpha);
                byte unassociatedAlpha = (byte)(((alpha * byte.MaxValue) + 127) / 254);

                source[index] = CreateNormalizedByte4P(associated, 0, 0, alpha);
                expectedUnassociated[index++] = new Bgra32(unassociated, 0, 0, unassociatedAlpha);
                source[index] = CreateNormalizedByte4P(0, associated, 0, alpha);
                expectedUnassociated[index++] = new Bgra32(0, unassociated, 0, unassociatedAlpha);
                source[index] = CreateNormalizedByte4P(0, 0, associated, alpha);
                expectedUnassociated[index++] = new Bgra32(0, 0, unassociated, unassociatedAlpha);
            }
        }

        PixelOperations<Bgra32>.Instance.From<NormalizedByte4P>(Configuration.Default, source, actualUnassociated);
        PixelOperations<NormalizedByte4P>.Instance.From<Bgra32>(Configuration.Default, actualUnassociated, actualRoundTrip);

        Assert.Equal(expectedUnassociated, actualUnassociated);
        Assert.Equal(source, actualRoundTrip);

        for (int i = 0; i < source.Length; i++)
        {
            Bgra32 expected = expectedUnassociated[i];

            // Scalar conversion and Color must preserve the same exact canonical value proven for the bulk path.
            Assert.Equal(new Rgba32(expected.R, expected.G, expected.B, expected.A), source[i].ToRgba32());
            Assert.Equal(expected, Color.FromPixel(source[i]).ToPixel<Bgra32>());
        }
    }

    private static void AssertUnsignedByteBulkRoundTrip<TPixel>(Func<byte, byte, byte, byte, TPixel> createPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const int pairCount = 32896;
        const int channelCount = 3;
        TPixel[] source = new TPixel[pairCount * channelCount];
        Bgra32[] expectedUnassociated = new Bgra32[source.Length];
        Bgra32[] actualUnassociated = new Bgra32[source.Length];
        TPixel[] actualRoundTrip = new TPixel[source.Length];
        int index = 0;

        for (int alpha = 0; alpha <= byte.MaxValue; alpha++)
        {
            // Components greater than alpha are valid for additive blending but cannot round-trip losslessly through an 8-bit unassociated pixel.
            // The exhaustive lossless domain is therefore triangular rather than 256 squared.
            for (int associated = 0; associated <= alpha; associated++)
            {
                // This integer expression is the exact nearest 8-bit unassociated value for associated * 255 / alpha.
                byte unassociated = alpha == 0 ? (byte)0 : (byte)(((associated * byte.MaxValue) + (alpha / 2)) / alpha);

                source[index] = createPixel((byte)associated, 0, 0, (byte)alpha);
                expectedUnassociated[index++] = new Bgra32(unassociated, 0, 0, (byte)alpha);
                source[index] = createPixel(0, (byte)associated, 0, (byte)alpha);
                expectedUnassociated[index++] = new Bgra32(0, unassociated, 0, (byte)alpha);
                source[index] = createPixel(0, 0, (byte)associated, (byte)alpha);
                expectedUnassociated[index++] = new Bgra32(0, 0, unassociated, (byte)alpha);
            }
        }

        PixelOperations<Bgra32>.Instance.From<TPixel>(Configuration.Default, source, actualUnassociated);
        PixelOperations<TPixel>.Instance.From<Bgra32>(Configuration.Default, actualUnassociated, actualRoundTrip);

        Assert.Equal(expectedUnassociated, actualUnassociated);
        Assert.Equal(source, actualRoundTrip);

        for (int i = 0; i < source.Length; i++)
        {
            Bgra32 expected = expectedUnassociated[i];

            // Scalar conversion and Color must use the same exact canonical byte as the independently calculated bulk oracle.
            Assert.Equal(new Rgba32(expected.R, expected.G, expected.B, expected.A), source[i].ToRgba32());
            Assert.Equal(expected, Color.FromPixel(source[i]).ToPixel<Bgra32>());
        }
    }

    private static void AssertUnsignedByteAssociation<TPixel>(Func<byte, byte, byte, byte, TPixel> createPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const int pairCount = 65536;
        const int channelCount = 3;
        Rgba32[] source = new Rgba32[pairCount * channelCount];
        TPixel[] expected = new TPixel[source.Length];
        TPixel[] actualBulk = new TPixel[source.Length];
        int index = 0;

        for (int alpha = 0; alpha <= byte.MaxValue; alpha++)
        {
            for (int unassociated = 0; unassociated <= byte.MaxValue; unassociated++)
            {
                // The destination stores round(unassociated * alpha / 255). The integer numerator adds half
                // the divisor so the expected value is independent of the floating-point implementation.
                byte associated = (byte)(((unassociated * alpha) + 127) / byte.MaxValue);

                source[index] = new Rgba32((byte)unassociated, 0, 0, (byte)alpha);
                expected[index++] = createPixel(associated, 0, 0, (byte)alpha);
                source[index] = new Rgba32(0, (byte)unassociated, 0, (byte)alpha);
                expected[index++] = createPixel(0, associated, 0, (byte)alpha);
                source[index] = new Rgba32(0, 0, (byte)unassociated, (byte)alpha);
                expected[index++] = createPixel(0, 0, associated, (byte)alpha);
            }
        }

        PixelOperations<TPixel>.Instance.From<Rgba32>(Configuration.Default, source, actualBulk);

        Assert.Equal(expected, actualBulk);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(expected[i], TPixel.FromRgba32(source[i]));
            Assert.Equal(expected[i], Color.FromPixel(source[i]).ToPixel<TPixel>());
        }
    }

    private static NormalizedByte4P CreateNormalizedByte4P(int red, int green, int blue, int alpha)
    {
        uint packed = (byte)(red - 127)
            | ((uint)(byte)(green - 127) << 8)
            | ((uint)(byte)(blue - 127) << 16)
            | ((uint)(byte)(alpha - 127) << 24);

        return new NormalizedByte4P { PackedValue = packed };
    }
}

/// <summary>
/// Tests that associated destinations use their stored alpha value when associating color components.
/// </summary>
public class AssociatedDestinationAlphaQuantizationTests
{
    [Fact]
    public void Rgba32PQuantizesDestinationAlphaBeforeAssociation() => AssertUnsignedByteDestinationQuantizesAlphaBeforeAssociation<Rgba32P>();

    [Fact]
    public void Bgra32PQuantizesDestinationAlphaBeforeAssociation() => AssertUnsignedByteDestinationQuantizesAlphaBeforeAssociation<Bgra32P>();

    [Fact]
    public void Argb32PQuantizesDestinationAlphaBeforeAssociation() => AssertUnsignedByteDestinationQuantizesAlphaBeforeAssociation<Argb32P>();

    [Fact]
    public void Abgr32PQuantizesDestinationAlphaBeforeAssociation() => AssertUnsignedByteDestinationQuantizesAlphaBeforeAssociation<Abgr32P>();

    [Fact]
    public void NormalizedByte4PQuantizesDestinationAlphaBeforeAssociation()
    {
        ReadOnlySpan<byte> components = [64, 127, 191];
        Rgba64[] source = new Rgba64[(ushort.MaxValue + 1) * components.Length];
        NormalizedByte4P[] actualBulk = new NormalizedByte4P[source.Length];
        int index = 0;

        for (int alpha = 0; alpha <= ushort.MaxValue; alpha++)
        {
            foreach (byte component in components)
            {
                source[index++] = new Rgba64((ushort)(component * 257), 0, 0, (ushort)alpha);
            }
        }

        PixelOperations<NormalizedByte4P>.Instance.From<Rgba64>(Configuration.Default, source, actualBulk);
        index = 0;

        for (int alpha = 0; alpha <= ushort.MaxValue; alpha++)
        {
            int expectedAlpha = ((alpha * 254) + 32767) / ushort.MaxValue;

            foreach (byte component in components)
            {
                int expectedRed = ((component * expectedAlpha) + 127) / byte.MaxValue;
                NormalizedByte4P actualScalar = NormalizedByte4P.FromRgba64(source[index]);
                int scalarRed = (sbyte)actualScalar.PackedValue + 127;
                int scalarAlpha = (sbyte)(actualScalar.PackedValue >> 24) + 127;
                int bulkRed = (sbyte)actualBulk[index].PackedValue + 127;
                int bulkAlpha = (sbyte)(actualBulk[index].PackedValue >> 24) + 127;

                Assert.Equal(expectedRed, scalarRed);
                Assert.Equal(expectedAlpha, scalarAlpha);
                Assert.Equal(expectedRed, bulkRed);
                Assert.Equal(expectedAlpha, bulkAlpha);
                index++;
            }
        }
    }

    [Fact]
    public void HalfVector4PQuantizesDestinationAlphaBeforeAssociation()
    {
        const float finiteMinimum = -65504F;
        const float finiteRange = 131008F;
        const float inverseFiniteRange = (float)(1D / finiteRange);
        ReadOnlySpan<byte> components = [64, 127, 191];
        Rgba64[] source = new Rgba64[(ushort.MaxValue + 1) * components.Length];
        HalfVector4P[] actualBulk = new HalfVector4P[source.Length];
        int index = 0;

        for (int alpha = 0; alpha <= ushort.MaxValue; alpha++)
        {
            foreach (byte component in components)
            {
                source[index++] = new Rgba64((ushort)(component * 257), 0, 0, (ushort)alpha);
            }
        }

        PixelOperations<HalfVector4P>.Instance.From<Rgba64>(Configuration.Default, source, actualBulk);
        index = 0;

        for (int alpha = 0; alpha <= ushort.MaxValue; alpha++)
        {
            float normalizedAlpha = alpha / (float)ushort.MaxValue;
            ushort expectedAlpha = BitConverter.HalfToUInt16Bits((Half)((normalizedAlpha * finiteRange) + finiteMinimum));
            float storedAlpha = ((float)BitConverter.UInt16BitsToHalf(expectedAlpha) * inverseFiniteRange) + .5F;

            foreach (byte component in components)
            {
                float associatedRed = (component / (float)byte.MaxValue) * storedAlpha;
                ushort expectedRed = BitConverter.HalfToUInt16Bits((Half)((associatedRed * finiteRange) + finiteMinimum));
                HalfVector4P actualScalar = HalfVector4P.FromRgba64(source[index]);

                Assert.Equal(expectedRed, (ushort)actualScalar.PackedValue);
                Assert.Equal(expectedAlpha, (ushort)(actualScalar.PackedValue >> 48));
                Assert.Equal(expectedRed, (ushort)actualBulk[index].PackedValue);
                Assert.Equal(expectedAlpha, (ushort)(actualBulk[index].PackedValue >> 48));
                index++;
            }
        }
    }

    [Fact]
    public void RgbaHalfPQuantizesDestinationAlphaBeforeAssociation()
    {
        ReadOnlySpan<byte> components = [64, 127, 191];
        Rgba64[] source = new Rgba64[(ushort.MaxValue + 1) * components.Length];
        RgbaHalfP[] actualBulk = new RgbaHalfP[source.Length];
        int index = 0;

        for (int alpha = 0; alpha <= ushort.MaxValue; alpha++)
        {
            foreach (byte component in components)
            {
                source[index++] = new Rgba64((ushort)(component * 257), 0, 0, (ushort)alpha);
            }
        }

        PixelOperations<RgbaHalfP>.Instance.From<Rgba64>(Configuration.Default, source, actualBulk);
        index = 0;

        for (int alpha = 0; alpha <= ushort.MaxValue; alpha++)
        {
            float normalizedAlpha = alpha / (float)ushort.MaxValue;
            Half expectedAlpha = (Half)normalizedAlpha;
            float storedAlpha = (float)expectedAlpha;

            foreach (byte component in components)
            {
                Half expectedRed = (Half)((component / (float)byte.MaxValue) * storedAlpha);
                RgbaHalfP actualScalar = RgbaHalfP.FromRgba64(source[index]);

                Assert.Equal(expectedRed, actualScalar.R);
                Assert.Equal(expectedAlpha, actualScalar.A);
                Assert.Equal(expectedRed, actualBulk[index].R);
                Assert.Equal(expectedAlpha, actualBulk[index].A);
                index++;
            }
        }
    }

    [Fact]
    public void ColorToRgba32PUsesDestinationAlphaRepresentation() => AssertColorUsesDestinationAlphaRepresentation<Rgba32P>();

    [Fact]
    public void ColorToBgra32PUsesDestinationAlphaRepresentation() => AssertColorUsesDestinationAlphaRepresentation<Bgra32P>();

    [Fact]
    public void ColorToArgb32PUsesDestinationAlphaRepresentation() => AssertColorUsesDestinationAlphaRepresentation<Argb32P>();

    [Fact]
    public void ColorToAbgr32PUsesDestinationAlphaRepresentation() => AssertColorUsesDestinationAlphaRepresentation<Abgr32P>();

    [Fact]
    public void ColorToNormalizedByte4PUsesDestinationAlphaRepresentation() => AssertColorUsesDestinationAlphaRepresentation<NormalizedByte4P>();

    [Fact]
    public void ColorToHalfVector4PUsesDestinationAlphaRepresentation() => AssertColorUsesDestinationAlphaRepresentation<HalfVector4P>();

    [Fact]
    public void ColorToRgbaHalfPUsesDestinationAlphaRepresentation() => AssertColorUsesDestinationAlphaRepresentation<RgbaHalfP>();

    /// <summary>
    /// Verifies the unsigned-byte destination grid through scalar and bulk conversion entry points.
    /// </summary>
    /// <typeparam name="TPixel">The associated unsigned-byte pixel format.</typeparam>
    private static void AssertUnsignedByteDestinationQuantizesAlphaBeforeAssociation<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ReadOnlySpan<byte> components = [64, 127, 191];
        Rgba64[] source = new Rgba64[(ushort.MaxValue + 1) * components.Length];
        TPixel[] actualBulk = new TPixel[source.Length];
        int index = 0;

        for (int alpha = 0; alpha <= ushort.MaxValue; alpha++)
        {
            foreach (byte component in components)
            {
                source[index++] = new Rgba64((ushort)(component * 257), 0, 0, (ushort)alpha);
            }
        }

        PixelOperations<TPixel>.Instance.From<Rgba64>(Configuration.Default, source, actualBulk);
        index = 0;

        for (int alpha = 0; alpha <= ushort.MaxValue; alpha++)
        {
            int expectedAlpha = ((alpha * byte.MaxValue) + 32767) / ushort.MaxValue;

            foreach (byte component in components)
            {
                int expectedRed = ((component * expectedAlpha) + 127) / byte.MaxValue;
                Vector4 expected = new Vector4(expectedRed, 0, 0, expectedAlpha) / byte.MaxValue;

                Assert.Equal(expected, TPixel.FromRgba64(source[index]).ToScaledVector4());
                Assert.Equal(expected, actualBulk[index].ToScaledVector4());
                index++;
            }
        }
    }

    /// <summary>
    /// Verifies that <see cref="Color"/> delegates association to the destination pixel operations.
    /// </summary>
    /// <typeparam name="TPixel">The associated destination pixel format.</typeparam>
    private static void AssertColorUsesDestinationAlphaRepresentation<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (int alpha = 0; alpha <= ushort.MaxValue; alpha++)
        {
            ushort component = (ushort)((byte)alpha * 257);
            Rgba64 source = new(component, 0, 0, (ushort)alpha);
            TPixel expected = TPixel.FromRgba64(source);
            TPixel actual = Color.FromPixel(source).ToPixel<TPixel>();

            Assert.Equal(expected, actual);
        }
    }
}
