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
    /// Separate-channel CDF lookups use the same endpoint bins for out-of-range and nonfinite samples.
    /// </summary>
    [Fact]
    public void SeparateChannels_FloatingSamplesMatchBoundedIndexReference()
    {
        float[] samples = [float.NaN, float.NegativeInfinity, -1F, 0F, 0.25F, 0.75F, 2F, float.PositiveInfinity];
        float[] bounded = [0F, 0F, 0F, 0F, 0.25F, 0.75F, 1F, 1F];
        using Image<RgbaVector> actual = new(17, 17);
        using Image<RgbaVector> reference = new(17, 17);

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                int sample = (x + y) % samples.Length;
                actual[x, y] = new RgbaVector(samples[sample], samples[sample], samples[sample], 0.625F);
                reference[x, y] = new RgbaVector(bounded[sample], bounded[sample], bounded[sample], 0.625F);
            }
        }

        AutoLevelProcessor processor = new(5, false, 350, false);

        actual.Mutate(ctx => ctx.ApplyProcessor(processor));
        reference.Mutate(ctx => ctx.ApplyProcessor(processor));

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                Assert.Equal(reference[x, y], actual[x, y]);
            }
        }
    }

    /// <summary>
    /// Synchronized AutoLevel uses the endpoint CDF entry while retaining the original HDR color for scaling.
    /// </summary>
    [Fact]
    public void SynchronizedChannels_UsesEndpointIndexWithoutClampingHdrColor()
    {
        float[] samples = [float.NaN, float.NegativeInfinity, 0.25F, 0.5F, 0.75F, 2F, float.PositiveInfinity];
        using Image<RgbaVector> image = new(samples.Length, 1);

        for (int x = 0; x < samples.Length; x++)
        {
            image[x, 0] = new RgbaVector(samples[x], samples[x], samples[x], 0.625F);
        }

        image.Mutate(ctx => ctx.ApplyProcessor(new AutoLevelProcessor(5, false, 350, true)));

        Assert.Equal(new RgbaVector(2.5F, 2.5F, 2.5F, 0.625F), image[5, 0]);
        Assert.True(float.IsPositiveInfinity(image[6, 0].ToVector4().X));
    }

    /// <summary>
    /// Synchronized AutoLevel maps black without division and scales HDR color from the endpoint CDF entry.
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
        Assert.Equal(new RgbaVector(0.625F, 0.625F, 0.625F, 0.625F), image[1, 0]);
        Assert.Equal(new RgbaVector(2.5F, 2.5F, 2.5F, 0.625F), image[2, 0]);
    }

    /// <summary>
    /// Separate-channel AutoLevel uses the endpoint CDF entry for HDR component values.
    /// </summary>
    [Fact]
    public void SeparateChannels_MapsHdrComponentsToEndpointBin()
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
