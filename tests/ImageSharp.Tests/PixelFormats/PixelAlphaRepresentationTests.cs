// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

/// <summary>
/// Verifies scalar and bulk alpha-representation conversions for a pixel format.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
[Trait("Category", "PixelFormats")]
public abstract class PixelAlphaRepresentationTests<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private static readonly Vector4[] UnassociatedScaledVectors =
    [
        new(.8F, .4F, .2F, .5F),
        new(.15F, .65F, .35F, .25F),
        new(.9F, .1F, .7F, 1F),
        Vector4.Zero
    ];

    private static readonly Vector4[] OutOfRangeUnassociatedScaledVectors =
    [
        new(-.25F, 1.25F, .5F, .75F),
        new(1.5F, -.5F, 2F, 1F)
    ];

    [Fact]
    public void ScalarScaledConversionsDescribeTheSameLogicalColors()
    {
        foreach (Vector4 unassociated in UnassociatedScaledVectors)
        {
            Vector4 associated = Associate(unassociated);
            TPixel fromUnassociated = TPixel.FromUnassociatedScaledVector4(unassociated);
            TPixel fromAssociated = TPixel.FromAssociatedScaledVector4(associated);

            Assert.Equal(fromUnassociated, fromAssociated);
        }
    }

    [Fact]
    public void ScalarAssociatedVectorsContainPremultipliedRgb()
    {
        foreach (Vector4 source in UnassociatedScaledVectors)
        {
            TPixel pixel = TPixel.FromUnassociatedScaledVector4(source);
            Vector4 unassociatedScaled = pixel.ToUnassociatedScaledVector4();
            Vector4 expectedAssociatedScaled = Associate(unassociatedScaled);
            Vector4 actualAssociatedScaled = pixel.ToAssociatedScaledVector4();

            if (typeof(TPixel) == typeof(NormalizedByte4P))
            {
                // Exhaustive valid-component coverage proves that signed-byte unassociation followed by reassociation can differ by at most two ULP.
                AlphaRepresentationTestAssertions.EqualWithinTwoUlps(expectedAssociatedScaled, actualAssociatedScaled);
            }
            else
            {
                // Reversing association can introduce one final float rounding step, but this still rejects an unassociated no-op by several orders of magnitude.
                AlphaRepresentationTestAssertions.EqualWithinOneUlp(expectedAssociatedScaled, actualAssociatedScaled);
            }

            Assert.Equal(BitConverter.SingleToInt32Bits(expectedAssociatedScaled.W), BitConverter.SingleToInt32Bits(actualAssociatedScaled.W));

            Vector4 unassociatedNative = pixel.ToUnassociatedVector4();

            if (unassociatedNative == unassociatedScaled)
            {
                // Native-equals-scaled formats use the same independent oracle; affine-native formats are verified by AffineNativeAlphaRepresentationTests.
                AlphaRepresentationTestAssertions.EqualWithinOneUlp(expectedAssociatedScaled, pixel.ToAssociatedVector4());
            }
        }
    }

    [Fact]
    public void ScalarScaledConversionsClampHighAlphaConsistently()
    {
        Vector4 unassociated = new(.8F, .4F, .2F, 1.5F);
        Vector4 associated = Associate(unassociated);

        // Both entry points describe the same logical color, so clamping opacity at the storage boundary must not change RGB differently between them.
        Assert.Equal(TPixel.FromUnassociatedScaledVector4(unassociated), TPixel.FromAssociatedScaledVector4(associated));
    }

    [Fact]
    public void ScalarNativeConversionsRoundTripTheirDeclaredRepresentations()
    {
        foreach (Vector4 source in UnassociatedScaledVectors)
        {
            TPixel pixel = TPixel.FromUnassociatedScaledVector4(source);
            TPixel fromUnassociated = TPixel.FromUnassociatedVector4(pixel.ToUnassociatedVector4());
            TPixel fromAssociated = TPixel.FromAssociatedVector4(pixel.ToAssociatedVector4());

            Assert.Equal(pixel, fromUnassociated);

            if (pixel.ToUnassociatedScaledVector4().W == 0F)
            {
                // Associated color cannot encode hidden RGB at zero alpha. The round trip must therefore produce the format's transparent black value.
                Assert.Equal(TPixel.FromUnassociatedScaledVector4(Vector4.Zero), fromAssociated);
            }
            else
            {
                Assert.Equal(pixel, fromAssociated);
            }
        }
    }

    [Fact]
    public void ParameterlessVectorConversionsUseTheDeclaredAlphaRepresentation()
    {
        PixelAlphaRepresentation alphaRepresentation = TPixel.GetPixelTypeInfo().AlphaRepresentation;

        foreach (Vector4 source in UnassociatedScaledVectors)
        {
            TPixel pixel = TPixel.FromUnassociatedScaledVector4(source);
            Vector4 expectedNative = alphaRepresentation == PixelAlphaRepresentation.Associated
                ? pixel.ToAssociatedVector4()
                : pixel.ToUnassociatedVector4();

            Vector4 expectedScaled = alphaRepresentation == PixelAlphaRepresentation.Associated
                ? pixel.ToAssociatedScaledVector4()
                : pixel.ToUnassociatedScaledVector4();

            TPixel expectedFromNative = alphaRepresentation == PixelAlphaRepresentation.Associated
                ? TPixel.FromAssociatedVector4(expectedNative)
                : TPixel.FromUnassociatedVector4(expectedNative);

            TPixel expectedFromScaled = alphaRepresentation == PixelAlphaRepresentation.Associated
                ? TPixel.FromAssociatedScaledVector4(expectedScaled)
                : TPixel.FromUnassociatedScaledVector4(expectedScaled);

            Assert.Equal(expectedNative, pixel.ToVector4());
            Assert.Equal(expectedScaled, pixel.ToScaledVector4());
            Assert.Equal(expectedFromNative, TPixel.FromVector4(expectedNative));
            Assert.Equal(expectedFromScaled, TPixel.FromScaledVector4(expectedScaled));
        }
    }

    [Fact]
    public void BulkConversionsMatchScalarConversions()
    {
        const int length = 259;
        TPixel[] pixels = new TPixel[length];

        for (int i = 0; i < pixels.Length; i++)
        {
            Vector4 source = UnassociatedScaledVectors[i % UnassociatedScaledVectors.Length];
            pixels[i] = TPixel.FromUnassociatedScaledVector4(source);
        }

        AssertBulkToMatchesScalar(pixels, static pixel => pixel.ToUnassociatedVector4(), static (operations, source, destination) => operations.ToVector4(Configuration.Default, source, destination, PixelConversionModifiers.UnPremultiply));
        AssertBulkToMatchesScalar(pixels, static pixel => pixel.ToAssociatedVector4(), static (operations, source, destination) => operations.ToVector4(Configuration.Default, source, destination, PixelConversionModifiers.Premultiply));
        AssertBulkToMatchesScalar(pixels, static pixel => pixel.ToUnassociatedScaledVector4(), static (operations, source, destination) => operations.ToVector4(Configuration.Default, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply));
        AssertBulkToMatchesScalar(pixels, static pixel => pixel.ToAssociatedScaledVector4(), static (operations, source, destination) => operations.ToVector4(Configuration.Default, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply));

        AssertBulkFromMatchesScalar(pixels, static pixel => pixel.ToUnassociatedVector4(), static vector => TPixel.FromUnassociatedVector4(vector), static (operations, source, destination) => operations.FromVector4Destructive(Configuration.Default, source, destination, PixelConversionModifiers.UnPremultiply));
        AssertBulkFromMatchesScalar(pixels, static pixel => pixel.ToAssociatedVector4(), static vector => TPixel.FromAssociatedVector4(vector), static (operations, source, destination) => operations.FromVector4Destructive(Configuration.Default, source, destination, PixelConversionModifiers.Premultiply));
        AssertBulkFromMatchesScalar(pixels, static pixel => pixel.ToUnassociatedScaledVector4(), static vector => TPixel.FromUnassociatedScaledVector4(vector), static (operations, source, destination) => operations.FromVector4Destructive(Configuration.Default, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply));
        AssertBulkFromMatchesScalar(pixels, static pixel => pixel.ToAssociatedScaledVector4(), static vector => TPixel.FromAssociatedScaledVector4(vector), static (operations, source, destination) => operations.FromVector4Destructive(Configuration.Default, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply));
    }

    [Fact]
    public void BulkScaledConversionsClampLikeScalarConversions()
    {
        Vector4[] associated = new Vector4[OutOfRangeUnassociatedScaledVectors.Length];

        for (int i = 0; i < associated.Length; i++)
        {
            associated[i] = Associate(OutOfRangeUnassociatedScaledVectors[i]);
        }

        AssertBulkFromMatchesScalar(OutOfRangeUnassociatedScaledVectors, static vector => TPixel.FromUnassociatedScaledVector4(vector), static (operations, source, destination) => operations.FromVector4Destructive(Configuration.Default, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply));
        AssertBulkFromMatchesScalar(associated, static vector => TPixel.FromAssociatedScaledVector4(vector), static (operations, source, destination) => operations.FromVector4Destructive(Configuration.Default, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BulkCompandingHonorsRequestedAlphaRepresentationAndRange(bool scaled, bool associated)
    {
        const int length = 259;
        TPixel[] pixels = new TPixel[length];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = TPixel.FromUnassociatedScaledVector4(UnassociatedScaledVectors[i % UnassociatedScaledVectors.Length]);
        }

        PixelConversionModifiers modifiers = PixelConversionModifiers.SRgbCompand
            | (scaled ? PixelConversionModifiers.Scale : PixelConversionModifiers.None)
            | (associated ? PixelConversionModifiers.Premultiply : PixelConversionModifiers.UnPremultiply);

        Vector4[] expectedVectors = new Vector4[length];

        for (int i = 0; i < pixels.Length; i++)
        {
            expectedVectors[i] = scaled ? pixels[i].ToUnassociatedScaledVector4() : pixels[i].ToUnassociatedVector4();
        }

        // Transfer functions operate on straight RGB. Association is therefore applied after expansion on the outbound path.
        SRgbCompanding.Expand(expectedVectors);

        if (associated)
        {
            Numerics.Premultiply(expectedVectors);
        }

        Vector4[] actualVectors = new Vector4[length];
        PixelOperations<TPixel>.Instance.ToVector4(Configuration.Default, pixels, actualVectors, modifiers);

        Assert.Equal(expectedVectors, actualVectors);

        Vector4[] expectedPixelSource = [.. expectedVectors];

        if (associated)
        {
            Numerics.UnPremultiply(expectedPixelSource);
        }

        // Reverse the transfer only after restoring straight RGB, matching the observable modifier order for inbound vectors.
        SRgbCompanding.Compress(expectedPixelSource);

        TPixel[] expectedPixels = new TPixel[length];

        for (int i = 0; i < expectedPixels.Length; i++)
        {
            expectedPixels[i] = scaled
                ? TPixel.FromUnassociatedScaledVector4(expectedPixelSource[i])
                : TPixel.FromUnassociatedVector4(expectedPixelSource[i]);
        }

        Vector4[] actualPixelSource = [.. expectedVectors];
        TPixel[] actualPixels = new TPixel[length];
        PixelOperations<TPixel>.Instance.FromVector4Destructive(Configuration.Default, actualPixelSource, actualPixels, modifiers);

        Assert.Equal(expectedPixels, actualPixels);
    }

    private static void AssertBulkToMatchesScalar(TPixel[] pixels, Func<TPixel, Vector4> scalar, BulkToVector4 bulk)
    {
        Vector4[] expected = new Vector4[pixels.Length];
        Vector4[] actual = new Vector4[pixels.Length + 3];
        Vector4 sentinel = new(.125F, .25F, .5F, .75F);

        for (int i = 0; i < pixels.Length; i++)
        {
            expected[i] = scalar(pixels[i]);
        }

        actual.AsSpan(pixels.Length).Fill(sentinel);
        bulk(PixelOperations<TPixel>.Instance, pixels, actual);

        // Bulk conversion is source-length driven and must leave excess destination capacity untouched.
        Assert.Equal(expected, actual[..pixels.Length]);
        Assert.All(actual[pixels.Length..], vector => Assert.Equal(sentinel, vector));
    }

    private static void AssertBulkFromMatchesScalar(TPixel[] pixels, Func<TPixel, Vector4> createSource, Func<Vector4, TPixel> scalar, BulkFromVector4 bulk)
    {
        Vector4[] source = new Vector4[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            source[i] = createSource(pixels[i]);
        }

        AssertBulkFromMatchesScalar(source, scalar, bulk);
    }

    private static void AssertBulkFromMatchesScalar(Vector4[] source, Func<Vector4, TPixel> scalar, BulkFromVector4 bulk)
    {
        TPixel[] expected = new TPixel[source.Length];
        TPixel[] actual = new TPixel[source.Length + 3];
        TPixel sentinel = TPixel.FromUnassociatedScaledVector4(new Vector4(.125F, .25F, .5F, .75F));

        for (int i = 0; i < source.Length; i++)
        {
            expected[i] = scalar(source[i]);
        }

        // From-vector bulk operations are destructive, so preserve the scalar source for diagnostics and future assertions.
        Vector4[] destructiveSource = [.. source];
        actual.AsSpan(source.Length).Fill(sentinel);
        bulk(PixelOperations<TPixel>.Instance, destructiveSource, actual);

        // A destination may expose spare capacity; the source span still defines how many pixels are written.
        Assert.Equal(expected, actual[..source.Length]);
        Assert.All(actual[source.Length..], pixel => Assert.Equal(sentinel, pixel));
    }

    private static Vector4 Associate(Vector4 vector)
    {
        vector.X *= vector.W;
        vector.Y *= vector.W;
        vector.Z *= vector.W;
        return vector;
    }

    private delegate void BulkToVector4(PixelOperations<TPixel> operations, ReadOnlySpan<TPixel> source, Span<Vector4> destination);

    private delegate void BulkFromVector4(PixelOperations<TPixel> operations, Span<Vector4> source, Span<TPixel> destination);
}

/// <summary>
/// Verifies the shared bulk vector conversion used by RGB byte formats.
/// </summary>
[Trait("Category", "PixelFormats")]
public class RgbaCompatiblePixelOperationsTests
{
    [Fact]
    public void BulkConversionsMatchScalarAcrossHardwareWidths() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertBulkConversionsMatchScalar,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    private static void AssertBulkConversionsMatchScalar()
    {
        AssertBulkConversionsMatchScalarForPixel<Bgr24>();
        AssertBulkConversionsMatchScalarForPixel<Rgb24>();
    }

    private static void AssertBulkConversionsMatchScalarForPixel<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const int length = 259;
        Vector4 sourceVector = new(.5F, .25F, .125F, 1F);
        TPixel expectedPixel = TPixel.FromUnassociatedScaledVector4(sourceVector);
        TPixel[] pixels = new TPixel[length];
        Array.Fill(pixels, expectedPixel);

        Vector4 expectedVector = expectedPixel.ToUnassociatedScaledVector4();
        Vector4 vectorSentinel = new(.125F, .5F, .75F, 1F);
        Vector4[] vectors = new Vector4[length + 3];
        vectors.AsSpan(length).Fill(vectorSentinel);

        PixelOperations<TPixel>.Instance.ToVector4(Configuration.Default, pixels, vectors, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

        Assert.All(vectors[..length], vector => Assert.Equal(expectedVector, vector));
        Assert.All(vectors[length..], vector => Assert.Equal(vectorSentinel, vector));

        Vector4[] source = new Vector4[length];
        source.AsSpan().Fill(sourceVector);

        TPixel pixelSentinel = TPixel.FromUnassociatedScaledVector4(vectorSentinel);
        TPixel[] destination = new TPixel[length + 3];
        destination.AsSpan(length).Fill(pixelSentinel);

        PixelOperations<TPixel>.Instance.FromVector4Destructive(Configuration.Default, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

        // A 259-pixel source crosses the 128- and 256-bit optimized thresholds while leaving spare destination capacity to detect writes beyond the source length.
        Assert.All(destination[..length], pixel => Assert.Equal(expectedPixel, pixel));
        Assert.All(destination[length..], pixel => Assert.Equal(pixelSentinel, pixel));
    }
}

/// <summary>
/// Verifies alpha conversion for formats with distinct native vector contracts.
/// </summary>
[Trait("Category", "PixelFormats")]
public class AffineNativeAlphaRepresentationTests
{
    private static readonly Vector4 UnassociatedScaled = new(.8F, .4F, .2F, .5F);

    [Fact]
    public void Byte4NativeConversionsUseScaledAlpha()
        => AssertNativeConversions<Byte4>(static vector => vector * byte.MaxValue);

    [Fact]
    public void Short4NativeConversionsUseScaledAlpha()
        => AssertNativeConversions<Short4>(static vector => (vector * ushort.MaxValue) - new Vector4(32768F));

    [Fact]
    public void NormalizedByte4NativeConversionsUseScaledAlpha()
        => AssertSignedNormalizedNativeConversions<NormalizedByte4>(static vector => (vector * 2F) - Vector4.One);

    [Fact]
    public void NormalizedByte4PNativeConversionsUseScaledAlpha()
        => AssertSignedNormalizedNativeConversions<NormalizedByte4P>(static vector => (vector * 2F) - Vector4.One);

    [Fact]
    public void NormalizedShort4NativeConversionsUseScaledAlpha()
        => AssertSignedNormalizedNativeConversions<NormalizedShort4>(static vector => (vector * 2F) - Vector4.One);

    [Fact]
    public void HalfVector4NativeConversionsUseScaledAlpha()
        => AssertNativeConversions<HalfVector4>(static vector => (vector * 131008F) - new Vector4(65504F));

    [Fact]
    public void HalfVector4PNativeConversionsUseScaledAlpha()
        => AssertNativeConversions<HalfVector4P>(static vector => (vector * 131008F) - new Vector4(65504F));

    [Fact]
    public void HalfSingleFromAssociatedNativeVectorUsesScaledAlpha()
        => AssertAlphaLessNativeFrom<HalfSingle>(static vector => new Vector4((vector.X * 131008F) - 65504F, 0F, 0F, vector.W));

    [Fact]
    public void HalfVector2FromAssociatedNativeVectorUsesScaledAlpha()
        => AssertAlphaLessNativeFrom<HalfVector2>(static vector => new Vector4((vector.X * 131008F) - 65504F, (vector.Y * 131008F) - 65504F, 0F, vector.W));

    [Fact]
    public void NormalizedByte2FromAssociatedNativeVectorUsesScaledAlpha()
        => AssertAlphaLessNativeFrom<NormalizedByte2>(static vector => new Vector4((vector.X * 2F) - 1F, (vector.Y * 2F) - 1F, 0F, vector.W));

    [Fact]
    public void NormalizedShort2FromAssociatedNativeVectorUsesScaledAlpha()
        => AssertAlphaLessNativeFrom<NormalizedShort2>(static vector => new Vector4((vector.X * 2F) - 1F, (vector.Y * 2F) - 1F, 0F, vector.W));

    [Fact]
    public void Short2FromAssociatedNativeVectorUsesScaledAlpha()
        => AssertAlphaLessNativeFrom<Short2>(static vector => new Vector4((vector.X * ushort.MaxValue) - 32768F, (vector.Y * ushort.MaxValue) - 32768F, 0F, vector.W));

    private static void AssertNativeConversions<TPixel>(Func<Vector4, Vector4> encodeNative)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel pixel = TPixel.FromUnassociatedScaledVector4(UnassociatedScaled);
        Vector4 unassociatedScaled = pixel.ToUnassociatedScaledVector4();
        Vector4 associatedScaled = Associate(unassociatedScaled);

        // Native alpha can be signed or use an integer component range, so multiplying native RGB by native W would not represent opacity.
        AlphaRepresentationTestAssertions.EqualWithinOneUlp(encodeNative(unassociatedScaled), pixel.ToUnassociatedVector4());
        AlphaRepresentationTestAssertions.EqualWithinOneUlp(encodeNative(associatedScaled), pixel.ToAssociatedVector4());
        Assert.Equal(pixel, TPixel.FromUnassociatedVector4(encodeNative(unassociatedScaled)));
        Assert.Equal(pixel, TPixel.FromAssociatedVector4(encodeNative(associatedScaled)));
    }

    private static void AssertSignedNormalizedNativeConversions<TPixel>(Func<Vector4, Vector4> encodeNative)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel pixel = TPixel.FromUnassociatedScaledVector4(UnassociatedScaled);
        Vector4 unassociatedScaled = pixel.ToUnassociatedScaledVector4();
        Vector4 associatedScaled = Associate(unassociatedScaled);

        // The 2*x-1 affine map is ill-conditioned around native zero, so a native-space ULP tolerance would not measure conversion accuracy.
        // Verify the stored native representation exactly, then verify both inverse paths using independently encoded scaled vectors.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Assert.Equal(pixel.ToVector4(), pixel.ToAssociatedVector4());
        }
        else
        {
            Assert.Equal(pixel.ToVector4(), pixel.ToUnassociatedVector4());
        }

        Assert.Equal(pixel, TPixel.FromUnassociatedVector4(encodeNative(unassociatedScaled)));
        Assert.Equal(pixel, TPixel.FromAssociatedVector4(encodeNative(associatedScaled)));
    }

    private static void AssertAlphaLessNativeFrom<TPixel>(Func<Vector4, Vector4> encodeNative)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel expected = TPixel.FromUnassociatedScaledVector4(UnassociatedScaled);
        Vector4 associatedScaled = Associate(UnassociatedScaled);

        // Formats with implicit alpha still need to unassociate an associated source before discarding its W component.
        Assert.Equal(expected, TPixel.FromAssociatedVector4(encodeNative(associatedScaled)));
    }

    private static Vector4 Associate(Vector4 vector)
    {
        vector.X *= vector.W;
        vector.Y *= vector.W;
        vector.Z *= vector.W;
        return vector;
    }
}

/// <summary>
/// Provides exact floating-point bounds for independently ordered alpha-association calculations.
/// </summary>
internal static class AlphaRepresentationTestAssertions
{
    /// <summary>
    /// Verifies that corresponding components differ by no more than one binary32 value.
    /// </summary>
    /// <param name="expected">The independently calculated expected vector.</param>
    /// <param name="actual">The vector produced by the pixel implementation.</param>
    internal static void EqualWithinOneUlp(Vector4 expected, Vector4 actual)
    {
        // One neighboring value is the strict bound for a single independently ordered, correctly rounded binary32 operation.
        Assert.InRange(actual.X, MathF.BitDecrement(expected.X), MathF.BitIncrement(expected.X));
        Assert.InRange(actual.Y, MathF.BitDecrement(expected.Y), MathF.BitIncrement(expected.Y));
        Assert.InRange(actual.Z, MathF.BitDecrement(expected.Z), MathF.BitIncrement(expected.Z));
        Assert.InRange(actual.W, MathF.BitDecrement(expected.W), MathF.BitIncrement(expected.W));
    }

    /// <summary>
    /// Verifies that corresponding components differ by no more than two binary32 values.
    /// </summary>
    /// <param name="expected">The independently calculated expected vector.</param>
    /// <param name="actual">The vector produced by the pixel implementation.</param>
    internal static void EqualWithinTwoUlps(Vector4 expected, Vector4 actual)
    {
        // Two neighboring values are the exhaustive bound for NormalizedByte4P unassociation followed by reassociation.
        // Bit increments express that exact bound without introducing a loose decimal tolerance.
        Assert.InRange(actual.X, MathF.BitDecrement(MathF.BitDecrement(expected.X)), MathF.BitIncrement(MathF.BitIncrement(expected.X)));
        Assert.InRange(actual.Y, MathF.BitDecrement(MathF.BitDecrement(expected.Y)), MathF.BitIncrement(MathF.BitIncrement(expected.Y)));
        Assert.InRange(actual.Z, MathF.BitDecrement(MathF.BitDecrement(expected.Z)), MathF.BitIncrement(MathF.BitIncrement(expected.Z)));
        Assert.InRange(actual.W, MathF.BitDecrement(MathF.BitDecrement(expected.W)), MathF.BitIncrement(MathF.BitIncrement(expected.W)));
    }
}

public class A8AlphaRepresentationTests : PixelAlphaRepresentationTests<A8> { }
public class Abgr32AlphaRepresentationTests : PixelAlphaRepresentationTests<Abgr32> { }
public class Abgr32PAlphaRepresentationTests : PixelAlphaRepresentationTests<Abgr32P> { }
public class Argb32AlphaRepresentationTests : PixelAlphaRepresentationTests<Argb32> { }
public class Argb32PAlphaRepresentationTests : PixelAlphaRepresentationTests<Argb32P> { }
public class Bgr24AlphaRepresentationTests : PixelAlphaRepresentationTests<Bgr24> { }
public class Bgr565AlphaRepresentationTests : PixelAlphaRepresentationTests<Bgr565> { }
public class Bgra32AlphaRepresentationTests : PixelAlphaRepresentationTests<Bgra32> { }
public class Bgra32PAlphaRepresentationTests : PixelAlphaRepresentationTests<Bgra32P> { }
public class Bgra4444AlphaRepresentationTests : PixelAlphaRepresentationTests<Bgra4444> { }
public class Bgra5551AlphaRepresentationTests : PixelAlphaRepresentationTests<Bgra5551> { }
public class Byte4AlphaRepresentationTests : PixelAlphaRepresentationTests<Byte4> { }
public class HalfSingleAlphaRepresentationTests : PixelAlphaRepresentationTests<HalfSingle> { }
public class HalfVector2AlphaRepresentationTests : PixelAlphaRepresentationTests<HalfVector2> { }
public class HalfVector4AlphaRepresentationTests : PixelAlphaRepresentationTests<HalfVector4> { }
public class HalfVector4PAlphaRepresentationTests : PixelAlphaRepresentationTests<HalfVector4P> { }
public class L16AlphaRepresentationTests : PixelAlphaRepresentationTests<L16> { }
public class L8AlphaRepresentationTests : PixelAlphaRepresentationTests<L8> { }
public class La16AlphaRepresentationTests : PixelAlphaRepresentationTests<La16> { }
public class La32AlphaRepresentationTests : PixelAlphaRepresentationTests<La32> { }
public class NormalizedByte2AlphaRepresentationTests : PixelAlphaRepresentationTests<NormalizedByte2> { }
public class NormalizedByte4AlphaRepresentationTests : PixelAlphaRepresentationTests<NormalizedByte4> { }
public class NormalizedByte4PAlphaRepresentationTests : PixelAlphaRepresentationTests<NormalizedByte4P> { }
public class NormalizedShort2AlphaRepresentationTests : PixelAlphaRepresentationTests<NormalizedShort2> { }
public class NormalizedShort4AlphaRepresentationTests : PixelAlphaRepresentationTests<NormalizedShort4> { }
public class Rg32AlphaRepresentationTests : PixelAlphaRepresentationTests<Rg32> { }
public class Rgb24AlphaRepresentationTests : PixelAlphaRepresentationTests<Rgb24> { }
public class Rgb48AlphaRepresentationTests : PixelAlphaRepresentationTests<Rgb48> { }
public class Rgb96AlphaRepresentationTests : PixelAlphaRepresentationTests<Rgb96> { }
public class Rgba1010102AlphaRepresentationTests : PixelAlphaRepresentationTests<Rgba1010102> { }
public class Rgba128AlphaRepresentationTests : PixelAlphaRepresentationTests<Rgba128> { }
public class Rgba32AlphaRepresentationTests : PixelAlphaRepresentationTests<Rgba32> { }
public class Rgba32PAlphaRepresentationTests : PixelAlphaRepresentationTests<Rgba32P> { }
public class RgbaHalfAlphaRepresentationTests : PixelAlphaRepresentationTests<RgbaHalf> { }
public class RgbaHalfPAlphaRepresentationTests : PixelAlphaRepresentationTests<RgbaHalfP> { }
public class Rgba64AlphaRepresentationTests : PixelAlphaRepresentationTests<Rgba64> { }
public class RgbaVectorAlphaRepresentationTests : PixelAlphaRepresentationTests<RgbaVector> { }
public class Short2AlphaRepresentationTests : PixelAlphaRepresentationTests<Short2> { }
public class Short4AlphaRepresentationTests : PixelAlphaRepresentationTests<Short4> { }
public class RgbaDoubleAlphaRepresentationTests : PixelAlphaRepresentationTests<RgbaDouble> { }
