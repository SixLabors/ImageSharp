// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Histograms;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Normalization;

// ReSharper disable InconsistentNaming
[Trait("Category", "Processors")]
public class HistogramEqualizationTests
{
    private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.0456F);

    /// <summary>
    /// Runs the published positive-infinity TIFF reproduction and its finite control through the public APIs.
    /// </summary>
    /// <remarks>
    /// Regression for <see href="https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-j3p4-wp97-rph4">CVE-2026-106113</see>.
    /// The advisory's TIFF samples caused an unsafe histogram access after decoding into HalfVector4.
    /// Keep both decoding and histogram equalization in this test: pixel conversion tests do not cover the complete crash path.
    /// </remarks>
    /// <param name="hex">The TIFF bytes produced by the advisory's reproduction or its 0.5 control.</param>
    [Theory]
    [InlineData(
        "49492A00080000000A0000010400010000000800000001010400010000000100000002010300010000002000000003010300010000000100" +
        "0000060103000100000001000000110104000100000086000000150103000100000001000000160104000100000001000000170104000100" +
        "000020000000530103000100000003000000000000000000807F0000807F0000807F0000807F0000807F0000807F0000807F0000807F")]
    [InlineData(
        "49492A00080000000A0000010400010000000800000001010400010000000100000002010300010000002000000003010300010000000100" +
        "0000060103000100000001000000110104000100000086000000150103000100000001000000160104000100000001000000170104000100" +
        "000020000000530103000100000003000000000000000000003F0000003F0000003F0000003F0000003F0000003F0000003F0000003F")]
    public void GlobalHistogramEqualization_Cve2026106113(string hex)
    {
        byte[] data = Convert.FromHexString(hex);
        using Image<HalfVector4> image = Image.Load<HalfVector4>(data);

        // This call terminated the process in the vulnerable releases. Do not replace it with an internal index test.
        image.Mutate(x => x.HistogramEqualization());

        Assert.Equal(new Size(8, 1), image.Size);
    }

    /// <summary>
    /// SDR measurements map to equal bin intervals, with midpoint ties rounded to the nearest even index.
    /// </summary>
    /// <param name="sample">The finite SDR measurement in [0,1].</param>
    /// <param name="expectedIndex">The expected index in a five-bin histogram.</param>
    [Theory]
    [InlineData(0F, 0)]
    [InlineData(0.125F, 0)]
    [InlineData(0.25F, 1)]
    [InlineData(0.375F, 2)]
    [InlineData(0.625F, 2)]
    [InlineData(0.75F, 3)]
    [InlineData(0.875F, 4)]
    [InlineData(1F, 4)]
    public void SdrLuminance_UsesLinearHistogramIndex(float sample, int expectedIndex)
    {
        HistogramIndexMapping mapping = new(5, -1);
        int index = mapping.Invoke(sample);

        Assert.Equal(expectedIndex, index);
    }

    /// <summary>
    /// Global equalization retains distinct extended magnitudes and uses finite pixel endpoints for nonfinite input.
    /// </summary>
    [Fact]
    public void GlobalFloatingSamples_UseDistinctExtendedBins()
    {
        float[] samples = [float.NaN, float.NegativeInfinity, -1F, 0.25F, 0.75F, 2F, float.PositiveInfinity];

        // NaN and negative infinity become the same finite minimum through the pixel API.
        // Its count is two; the five remaining values occupy distinct bins. Subtracting that
        // first count leaves cumulative values 0..5 divided by the remaining five samples.
        float[] expected = [0F, 0F, 0.2F, 0.4F, 0.6F, 0.8F, 1F];
        using Image<RgbaVector> image = new(samples.Length, 1);

        for (int x = 0; x < samples.Length; x++)
        {
            image[x, 0] = new RgbaVector(samples[x], samples[x], samples[x], 0.625F);
        }

        image.Mutate(ctx => ctx.HistogramEqualization(new HistogramEqualizationOptions
        {
            Method = HistogramEqualizationMethod.Global,
            LuminanceLevels = 5
        }));

        for (int x = 0; x < samples.Length; x++)
        {
            Assert.Equal(new RgbaVector(expected[x], expected[x], expected[x], 0.625F), image[x, 0]);
        }
    }

    /// <summary>
    /// Adaptive equalization preserves rank order when distinct extended input magnitudes are doubled.
    /// </summary>
    /// <param name="method">The equalization method.</param>
    [Theory]
    [InlineData(HistogramEqualizationMethod.AdaptiveTileInterpolation)]
    [InlineData(HistogramEqualizationMethod.AdaptiveSlidingWindow)]
    public void FloatingSamples_PreserveExtendedRankOrder(HistogramEqualizationMethod method)
    {
        // Doubling these distinct magnitudes preserves their order and histogram counts.
        // A clipped SDR mapping merges different entries and does not satisfy this property.
        float[] samples = [-8F, -2F, 0F, 0.125F, 0.5F, 1F, 2F, 4F];
        using Image<RgbaVector> actual = new(17, 17);
        using Image<RgbaVector> reference = new(17, 17);

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                int sample = (x + y) % samples.Length;
                actual[x, y] = new RgbaVector(samples[sample], samples[sample], samples[sample], 0.625F);
                reference[x, y] = new RgbaVector(samples[sample] * 2F, samples[sample] * 2F, samples[sample] * 2F, 0.625F);
            }
        }

        HistogramEqualizationOptions options = new()
        {
            Method = method,
            LuminanceLevels = 5,
            NumberOfTiles = 2
        };

        actual.Mutate(ctx => ctx.HistogramEqualization(options));
        reference.Mutate(ctx => ctx.HistogramEqualization(options));

        ImageComparer.Exact.VerifySimilarity(reference, actual);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(65536)]
    public void GlobalHistogramEqualization_WithDifferentLuminanceLevels(int luminanceLevels)
    {
        // Arrange
        byte[] pixels =
        [
            52,  55,  61,  59,  70,  61,  76,  61,
            62,  59,  55, 104,  94,  85,  59,  71,
            63,  65,  66, 113, 144, 104,  63,  72,
            64,  70,  70, 126, 154, 109,  71,  69,
            67,  73,  68, 106, 122,  88,  68,  68,
            68,  79,  60,  79,  77,  66,  58,  75,
            69,  85,  64,  58,  55,  61,  65,  83,
            70,  87,  69,  68,  65,  73,  78,  90
        ];

        using (Image<Rgba32> image = new(8, 8))
        {
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    byte luminance = pixels[(y * 8) + x];
                    image[x, y] = new Rgba32(luminance, luminance, luminance);
                }
            }

            byte[] expected =
            [
                0,    12,   53,   32,  146,   53,  174,   53,
                57,   32,   12,  227,  219,  202,   32,  154,
                65,   85,   93,  239,  251,  227,   65,  158,
                73,  146,  146,  247,  255,  235,  154,  130,
                97,  166,  117,  231,  243,  210,  117,  117,
                117, 190,   36,  190,  178,   93,   20,  170,
                130, 202,   73,   20,   12,   53,   85,  194,
                146, 206,  130,  117,   85,  166,  182,  215
            ];

            // Act
            image.Mutate(x => x.HistogramEqualization(new HistogramEqualizationOptions
            {
                LuminanceLevels = luminanceLevels,
                Method = HistogramEqualizationMethod.Global
            }));

            // Assert
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    Rgba32 actual = image[x, y];
                    Assert.Equal(expected[(y * 8) + x], actual.R);
                    Assert.Equal(expected[(y * 8) + x], actual.G);
                    Assert.Equal(expected[(y * 8) + x], actual.B);
                }
            }
        }
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.HistogramEqImage, PixelTypes.Rgba32)]
    public void GlobalHistogramEqualization_CompareToReferenceOutput<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            HistogramEqualizationOptions options = new()
            {
                Method = HistogramEqualizationMethod.Global,
                LuminanceLevels = 256,
            };
            image.Mutate(x => x.HistogramEqualization(options));
            image.DebugSave(provider);
            image.CompareToReferenceOutput(ValidatorComparer, provider, extension: "png");
        }
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.LowContrast, PixelTypes.Rgba32)]
    public void Adaptive_SlidingWindow_15Tiles_WithClipping<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            HistogramEqualizationOptions options = new()
            {
                Method = HistogramEqualizationMethod.AdaptiveSlidingWindow,
                LuminanceLevels = 256,
                ClipHistogram = true,
                NumberOfTiles = 15
            };
            image.Mutate(x => x.HistogramEqualization(options));
            image.DebugSave(provider);
            image.CompareToReferenceOutput(ValidatorComparer, provider);
        }
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.LowContrast, PixelTypes.Rgba32)]
    public void Adaptive_TileInterpolation_10Tiles_WithClipping<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            HistogramEqualizationOptions options = new()
            {
                Method = HistogramEqualizationMethod.AdaptiveTileInterpolation,
                LuminanceLevels = 256,
                ClipHistogram = true,
                NumberOfTiles = 10
            };
            image.Mutate(x => x.HistogramEqualization(options));
            image.DebugSave(provider);
            image.CompareToReferenceOutput(ValidatorComparer, provider);
        }
    }

    /// <summary>
    /// This is regression test for a bug with the calculation of the y-start positions,
    /// where it could happen that one too much start position was calculated in some cases.
    /// See: https://github.com/SixLabors/ImageSharp/pull/984
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
    /// <param name="provider">The test image provider.</param>
    [Theory]
    [WithTestPatternImages(110, 110, PixelTypes.Rgb24)]
    [WithTestPatternImages(170, 170, PixelTypes.Rgb24)]
    public void Issue984<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            HistogramEqualizationOptions options = new()
            {
                Method = HistogramEqualizationMethod.AdaptiveTileInterpolation,
                LuminanceLevels = 256,
                ClipHistogram = true,
                ClipLimit = 5,
                NumberOfTiles = 10
            };
            image.Mutate(x => x.HistogramEqualization(options));
            image.DebugSave(provider);
            image.CompareToReferenceOutput(ValidatorComparer, provider);
        }
    }

    [Theory]
    [WithTestPatternImages(5120, 9234, PixelTypes.L16)]
    public unsafe void Issue1640<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!TestEnvironment.Is64BitProcess)
        {
            return;
        }

        // https://github.com/SixLabors/ImageSharp/discussions/1640
        // Test using isolated memory to ensure clean buffers for reference
        provider.Configuration = Configuration.CreateDefaultInstance();
        HistogramEqualizationOptions options = new()
        {
            Method = HistogramEqualizationMethod.AdaptiveTileInterpolation,
            LuminanceLevels = 4096,
            ClipHistogram = false,
            ClipLimit = 350,
            NumberOfTiles = 8
        };

        using Image<TPixel> image = provider.GetImage();
        using Image<TPixel> referenceResult = image.Clone(ctx =>
        {
            ctx.HistogramEqualization(options);
            ctx.Resize(image.Width / 4, image.Height / 4, KnownResamplers.Bicubic);
        });

        using Image<TPixel> processed = image.Clone(ctx =>
        {
            ctx.HistogramEqualization(options);
            ctx.Resize(image.Width / 4, image.Height / 4, KnownResamplers.Bicubic);
        });

        ValidatorComparer.VerifySimilarity(referenceResult, processed);
    }
}
