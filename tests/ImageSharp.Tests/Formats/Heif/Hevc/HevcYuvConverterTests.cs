// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies the shared SIMD-first HEVC component conversion pipeline in both directions.
/// </summary>
[Trait("Format", "Heic")]
public class HevcYuvConverterTests
{
    /// <summary>
    /// Verifies that the identity matrix preserves every eight-bit RGB component through the HEVC GBR plane order.
    /// </summary>
    [Fact]
    public void IdentityRoundTripIsExact()
    {
        const int width = 19;
        const int height = 7;
        CicpProfile profile = CreateProfile(CicpMatrixCoefficients.Identity, true);
        using Image<Rgba32> source = new(width, height);
        for (int y = 0; y < height; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                row[x] = new Rgba32((byte)((x * 17) + y), (byte)((y * 31) + x), (byte)((x * 7) + (y * 13)));
            }
        }

        using HevcPictureBuffer picture = new(Configuration.Default, width, height, 8, 8, 3, false);
        HevcYuvConverter.ConvertFromRgb(
            Configuration.Default,
            source.Frames.RootFrame,
            picture,
            profile,
            HevcChromaSampleLocation.Left);

        using Image<Rgba32> destination = new(width, height);
        HevcYuvConverter.ConvertToRgb(
            Configuration.Default,
            picture,
            destination.Frames.RootFrame,
            profile,
            HevcChromaSampleLocation.Left);

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<Rgba32> expected = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            ReadOnlySpan<Rgba32> actual = destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            Assert.True(expected.SequenceEqual(actual));
        }
    }

    /// <summary>
    /// Verifies that every exposed precision and plane layout executes both conversion directions on an odd-sized image.
    /// </summary>
    /// <param name="bitDepth">The encoded component precision.</param>
    /// <param name="chromaFormat">The HEVC chroma-format identifier.</param>
    /// <param name="fullRange">Whether components use their complete numeric range.</param>
    [Theory]
    [InlineData(8, 0, true)]
    [InlineData(8, 1, false)]
    [InlineData(10, 2, true)]
    [InlineData(12, 3, false)]
    public void ConstantColorRoundTripsEveryPrecisionAndPlaneLayout(int bitDepth, byte chromaFormat, bool fullRange)
    {
        const int width = 17;
        const int height = 9;
        Rgba64 expected = new(38550, 25700, 51400, ushort.MaxValue);
        CicpProfile profile = CreateProfile(CicpMatrixCoefficients.ItuRBt709_6, fullRange);
        using Image<Rgba64> source = new(width, height, expected);
        using HevcPictureBuffer picture = new(Configuration.Default, width, height, bitDepth, bitDepth, chromaFormat, false);

        HevcYuvConverter.ConvertFromRgb(
            Configuration.Default,
            source.Frames.RootFrame,
            picture,
            profile,
            HevcChromaSampleLocation.Left);

        using Image<Rgba64> destination = new(width, height);
        HevcYuvConverter.ConvertToRgb(
            Configuration.Default,
            picture,
            destination.Frames.RootFrame,
            profile,
            HevcChromaSampleLocation.Left);

        int tolerance = bitDepth switch
        {
            8 => 800,
            10 => 240,
            _ => 80,
        };

        foreach (Rgba64 actual in destination.Frames.RootFrame.PixelBuffer.DangerousGetSingleSpan())
        {
            if (chromaFormat == 0)
            {
                // A monochrome picture retains only luma. The decoded channels must therefore agree, while
                // comparing them with the chromatic source would incorrectly require discarded color to survive.
                Assert.InRange(Math.Abs(actual.R - actual.G), 0, tolerance);
                Assert.InRange(Math.Abs(actual.R - actual.B), 0, tolerance);
                Assert.Equal(ushort.MaxValue, actual.A);
                continue;
            }

            Assert.InRange(Math.Abs(actual.R - expected.R), 0, tolerance);
            Assert.InRange(Math.Abs(actual.G - expected.G), 0, tolerance);
            Assert.InRange(Math.Abs(actual.B - expected.B), 0, tolerance);
            Assert.Equal(ushort.MaxValue, actual.A);
        }
    }

    /// <summary>
    /// Verifies the six HEVC 4:2:0 sample locations in both conversion directions.
    /// </summary>
    /// <param name="chromaSampleLocationValue">The signaled sample-location code point.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ConvertsEveryChromaSampleLocation(int chromaSampleLocationValue)
    {
        const int width = 17;
        const int height = 9;
        Rgba64 expected = new(46260, 20560, 33410, ushort.MaxValue);
        CicpProfile profile = CreateProfile(CicpMatrixCoefficients.ItuRBt709_6, true);
        HevcChromaSampleLocation chromaSampleLocation = (HevcChromaSampleLocation)chromaSampleLocationValue;
        using Image<Rgba64> source = new(width, height, expected);
        using HevcPictureBuffer picture = new(Configuration.Default, width, height, 10, 10, 1, false);

        HevcYuvConverter.ConvertFromRgb(Configuration.Default, source.Frames.RootFrame, picture, profile, chromaSampleLocation);

        using Image<Rgba64> destination = new(width, height);
        HevcYuvConverter.ConvertToRgb(Configuration.Default, picture, destination.Frames.RootFrame, profile, chromaSampleLocation);

        Rgba64 actual = destination[width - 1, height - 1];
        Assert.InRange(Math.Abs(actual.R - expected.R), 0, 240);
        Assert.InRange(Math.Abs(actual.G - expected.G), 0, 240);
        Assert.InRange(Math.Abs(actual.B - expected.B), 0, 240);
    }

    /// <summary>
    /// Verifies that encoding selects the horizontal and vertical sample coordinates defined by all six location code points.
    /// </summary>
    [Fact]
    public void ChromaSampleLocationsSelectExpectedSourceCoordinatesWhenEncoding()
    {
        CicpProfile profile = CreateProfile(CicpMatrixCoefficients.YCgCo, true);
        using Image<Rgba32> source = new(2, 2);
        source[0, 0] = new Rgba32(0, 0, 0);
        source[1, 0] = new Rgba32(0, byte.MaxValue, 0);
        source[0, 1] = new Rgba32(byte.MaxValue, 0, 0);
        source[1, 1] = new Rgba32(0, 0, byte.MaxValue);

        // YCgCo maps the four source pixels to neutral, positive Cg, positive Co, and negative Co. Applying the
        // H.273 full-range scale after the location-specific point or average yields these exact encoded samples.
        ReadOnlySpan<ushort> expectedChromaBlue = [96, 128, 128, 192, 64, 64];
        ReadOnlySpan<ushort> expectedChromaRed = [192, 128, 128, 128, 255, 128];
        for (int location = 0; location < expectedChromaBlue.Length; location++)
        {
            using HevcPictureBuffer picture = new(Configuration.Default, 2, 2, 8, 8, 1, false);
            HevcYuvConverter.ConvertFromRgb(
                Configuration.Default,
                source.Frames.RootFrame,
                picture,
                profile,
                (HevcChromaSampleLocation)location);

            Assert.Equal(expectedChromaBlue[location], picture.GetRowSpan(HevcPlane.Cb, 0)[0]);
            Assert.Equal(expectedChromaRed[location], picture.GetRowSpan(HevcPlane.Cr, 0)[0]);
        }
    }

    /// <summary>
    /// Verifies that decoding reconstructs chroma at the coordinates defined by all six location code points.
    /// </summary>
    [Fact]
    public void ChromaSampleLocationsInterpolateExpectedCoordinatesWhenDecoding()
    {
        CicpProfile profile = CreateProfile(CicpMatrixCoefficients.YCgCo, true);

        // At luma coordinate (1, 1), the six locations exercise centered and co-sited horizontal reconstruction
        // together with the quarter-sample weights for centered, top, and bottom vertical positions.
        ReadOnlySpan<byte> expectedRed = [96, 128, 128, 160, 64, 96];
        ReadOnlySpan<byte> expectedGreen = [160, 128, 128, 96, 192, 160];
        for (int location = 0; location < expectedRed.Length; location++)
        {
            using HevcPictureBuffer picture = new(Configuration.Default, 4, 4, 8, 8, 1, false);
            picture.Luma.DangerousGetSingleSpan().Fill(128);
            picture.ChromaRed!.DangerousGetSingleSpan().Fill(128);
            Span<ushort> topChromaBlue = picture.GetRowSpan(HevcPlane.Cb, 0);
            Span<ushort> bottomChromaBlue = picture.GetRowSpan(HevcPlane.Cb, 1);
            topChromaBlue[0] = 128;
            topChromaBlue[1] = 255;
            bottomChromaBlue[0] = 0;
            bottomChromaBlue[1] = 128;

            using Image<Rgba32> destination = new(4, 4);
            HevcYuvConverter.ConvertToRgb(
                Configuration.Default,
                picture,
                destination.Frames.RootFrame,
                profile,
                (HevcChromaSampleLocation)location);

            Rgba32 actual = destination[1, 1];
            Assert.InRange(Math.Abs(actual.R - expectedRed[location]), 0, 1);
            Assert.InRange(Math.Abs(actual.G - expectedGreen[location]), 0, 1);
            Assert.InRange(Math.Abs(actual.B - expectedRed[location]), 0, 1);
        }
    }

    /// <summary>
    /// Verifies that independent luma and chroma precisions use their own H.273 code ranges.
    /// </summary>
    [Fact]
    public void SupportsIndependentLumaAndChromaBitDepths()
    {
        CicpProfile profile = CreateProfile(CicpMatrixCoefficients.ItuRBt709_6, false);
        using Image<Rgba64> source = new(17, 5, new Rgba64(40000, 24000, 48000, ushort.MaxValue));
        using HevcPictureBuffer picture = new(Configuration.Default, 17, 5, 10, 12, 3, false);

        HevcYuvConverter.ConvertFromRgb(
            Configuration.Default,
            source.Frames.RootFrame,
            picture,
            profile,
            HevcChromaSampleLocation.Left);

        foreach (ushort sample in picture.Luma.DangerousGetSingleSpan())
        {
            Assert.InRange(sample, (ushort)0, (ushort)1023);
        }

        foreach (ushort sample in picture.ChromaBlue!.DangerousGetSingleSpan())
        {
            Assert.InRange(sample, (ushort)0, (ushort)4095);
        }

        foreach (ushort sample in picture.ChromaRed!.DangerousGetSingleSpan())
        {
            Assert.InRange(sample, (ushort)0, (ushort)4095);
        }

        using Image<Rgba64> destination = new(17, 5);
        HevcYuvConverter.ConvertToRgb(
            Configuration.Default,
            picture,
            destination.Frames.RootFrame,
            profile,
            HevcChromaSampleLocation.Left);
    }

    /// <summary>
    /// Verifies the exact limited-range endpoints written for ten-bit luma and chroma samples.
    /// </summary>
    [Fact]
    public void LimitedRangeUsesExactTenBitEndpoints()
    {
        CicpProfile profile = CreateProfile(CicpMatrixCoefficients.ItuRBt709_6, false);
        using Image<Rgba64> source = new(2, 1);
        source[0, 0] = new Rgba64(0, 0, 0, ushort.MaxValue);
        source[1, 0] = new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
        using HevcPictureBuffer picture = new(Configuration.Default, 2, 1, 10, 10, 3, false);

        HevcYuvConverter.ConvertFromRgb(
            Configuration.Default,
            source.Frames.RootFrame,
            picture,
            profile,
            HevcChromaSampleLocation.Left);

        Assert.Equal((ushort)64, picture.GetRowSpan(HevcPlane.Y, 0)[0]);
        Assert.Equal((ushort)940, picture.GetRowSpan(HevcPlane.Y, 0)[1]);
        Assert.Equal((ushort)512, picture.GetRowSpan(HevcPlane.Cb, 0)[0]);
        Assert.Equal((ushort)512, picture.GetRowSpan(HevcPlane.Cb, 0)[1]);
        Assert.Equal((ushort)512, picture.GetRowSpan(HevcPlane.Cr, 0)[0]);
        Assert.Equal((ushort)512, picture.GetRowSpan(HevcPlane.Cr, 0)[1]);
    }

    /// <summary>
    /// Creates a complete H.273 profile for a conversion test.
    /// </summary>
    /// <param name="matrixCoefficients">The matrix-coefficient code point.</param>
    /// <param name="fullRange">Whether components use their complete numeric range.</param>
    /// <returns>The configured profile.</returns>
    private static CicpProfile CreateProfile(CicpMatrixCoefficients matrixCoefficients, bool fullRange)
        => new(
            (byte)CicpColorPrimaries.ItuRBt709_6,
            (byte)CicpTransferCharacteristics.ItuRBt709_6,
            (byte)matrixCoefficients,
            fullRange);
}
