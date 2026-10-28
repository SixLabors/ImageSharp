// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Normalization;

[Trait("Category", "Processors")]
public class AutoLevelProcessorTests
{
    private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.0456F);

    /// <summary>
    /// The one-call operation expands a simple grayscale range while preserving alpha.
    /// </summary>
    [Fact]
    public void AutoLevel_AdjustsImageWithOneCall()
    {
        using Image<RgbaVector> image = new(3, 1);
        image[0, 0] = new RgbaVector(0F, 0F, 0F, 0.625F);
        image[1, 0] = new RgbaVector(0.25F, 0.25F, 0.25F, 0.625F);
        image[2, 0] = new RgbaVector(0.5F, 0.5F, 0.5F, 0.625F);

        image.Mutate(ctx => ctx.AutoLevel());

        Assert.Equal(new RgbaVector(0F, 0F, 0F, 0.625F), image[0, 0]);
        Assert.Equal(new RgbaVector(0.5F, 0.5F, 0.5F, 0.625F), image[1, 0]);
        Assert.Equal(new RgbaVector(1F, 1F, 1F, 0.625F), image[2, 0]);
    }

    /// <summary>
    /// Separate-channel levels preserve histogram order when distinct extended samples are doubled.
    /// </summary>
    [Fact]
    public void SeparateChannels_ExtendedSamplesPreserveRankOrder()
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

        AutoLevelProcessor processor = new(5, false, 350, false);

        actual.Mutate(ctx => ctx.ApplyProcessor(processor));
        reference.Mutate(ctx => ctx.ApplyProcessor(processor));

        ImageComparer.Exact.VerifySimilarity(reference, actual);
    }

    /// <summary>
    /// Synchronized levels use distinct HDR bins and the pixel API's finite nonfinite-input endpoints.
    /// </summary>
    [Fact]
    public void SynchronizedChannels_UsesExtendedBinsAndFinitePixelEndpoints()
    {
        float[] samples = [float.NaN, float.NegativeInfinity, 0.25F, 0.5F, 0.75F, 2F, float.PositiveInfinity];
        using Image<RgbaVector> image = new(samples.Length, 1);

        for (int x = 0; x < samples.Length; x++)
        {
            image[x, 0] = new RgbaVector(samples[x], samples[x], samples[x], 0.625F);
        }

        image.Mutate(ctx => ctx.ApplyProcessor(new AutoLevelProcessor(5, false, 350, true)));

        // Two inputs share the finite minimum. Subtracting their count leaves five samples:
        // the 2.0 sample has cumulative count four and the finite maximum has count five.
        Assert.Equal(new RgbaVector(0.8F, 0.8F, 0.8F, 0.625F), image[5, 0]);
        Assert.Equal(new RgbaVector(1F, 1F, 1F, 0.625F), image[6, 0]);
    }

    /// <summary>
    /// Synchronized levels map three distinct gray samples to cumulative values zero, one half and one.
    /// </summary>
    [Fact]
    public void SynchronizedChannels_MapsBlackAndHdrColor()
    {
        using Image<RgbaVector> image = new(3, 1);
        image[0, 0] = new RgbaVector(0F, 0F, 0F, 0.625F);
        image[1, 0] = new RgbaVector(0.5F, 0.5F, 0.5F, 0.625F);
        image[2, 0] = new RgbaVector(2F, 2F, 2F, 0.625F);

        image.Mutate(ctx => ctx.ApplyProcessor(new AutoLevelProcessor(5, false, 350, true)));

        Assert.Equal(new RgbaVector(0F, 0F, 0F, 0.625F), image[0, 0]);
        Assert.Equal(new RgbaVector(0.5F, 0.5F, 0.5F, 0.625F), image[1, 0]);
        Assert.Equal(new RgbaVector(1F, 1F, 1F, 0.625F), image[2, 0]);
    }

    /// <summary>
    /// Separate-channel levels use the distinct HDR sample's cumulative count.
    /// </summary>
    [Fact]
    public void SeparateChannels_MapsDistinctHdrComponents()
    {
        using Image<RgbaVector> image = new(3, 1);
        image[0, 0] = new RgbaVector(0F, 0F, 0F, 0.625F);
        image[1, 0] = new RgbaVector(0.5F, 0.5F, 0.5F, 0.625F);
        image[2, 0] = new RgbaVector(2F, 2F, 2F, 0.625F);

        image.Mutate(ctx => ctx.ApplyProcessor(new AutoLevelProcessor(5, false, 350, false)));

        Assert.Equal(new RgbaVector(0F, 0F, 0F, 0.625F), image[0, 0]);
        Assert.Equal(new RgbaVector(0.5F, 0.5F, 0.5F, 0.625F), image[1, 0]);
        Assert.Equal(new RgbaVector(1F, 1F, 1F, 0.625F), image[2, 0]);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.ForestBridgeDifferentComponentsQuality, PixelTypes.Rgba32)]
    public void SeparateChannels_CompareToReferenceOutput<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            image.Mutate(x => x.ApplyProcessor(new AutoLevelProcessor(256, false, 350, false)));
            image.DebugSave(provider);
            image.CompareToReferenceOutput(ValidatorComparer, provider, extension: "png");
        }
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.ForestBridgeDifferentComponentsQuality, PixelTypes.Rgba32)]
    public void SynchronizedChannels_CompareToReferenceOutput<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            image.Mutate(x => x.AutoLevel());
            image.DebugSave(provider);
            image.CompareToReferenceOutput(ValidatorComparer, provider, extension: "png");
        }
    }
}
