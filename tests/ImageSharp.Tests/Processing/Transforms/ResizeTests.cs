// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms;

[Trait("Category", "Processors")]
public class ResizeTests : BaseImageOperationsExtensionTest
{
    [Fact]
    public void ResizeWidthAndHeight()
    {
        int width = 50;
        int height = 100;
        this.operations.Resize(width, height);
        ResizeProcessor resizeProcessor = this.Verify<ResizeProcessor>();

        Assert.Equal(width, resizeProcessor.DestinationWidth);
        Assert.Equal(height, resizeProcessor.DestinationHeight);
    }

    [Fact]
    public void ResizeWidthAndHeightAndSampler()
    {
        int width = 50;
        int height = 100;
        IResampler sampler = KnownResamplers.Lanczos3;
        this.operations.Resize(width, height, sampler);
        ResizeProcessor resizeProcessor = this.Verify<ResizeProcessor>();

        Assert.Equal(width, resizeProcessor.DestinationWidth);
        Assert.Equal(height, resizeProcessor.DestinationHeight);
        Assert.Equal(sampler, resizeProcessor.Options.Sampler);
    }

    [Fact]
    public void ResizeWidthAndHeightAndSamplerAndCompand()
    {
        int width = 50;
        int height = 100;
        IResampler sampler = KnownResamplers.Lanczos3;
        bool compand = true;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        this.operations.Resize(width, height, sampler, compand);
        ResizeProcessor resizeProcessor = this.Verify<ResizeProcessor>();

        Assert.Equal(width, resizeProcessor.DestinationWidth);
        Assert.Equal(height, resizeProcessor.DestinationHeight);
        Assert.Equal(sampler, resizeProcessor.Options.Sampler);
        Assert.Equal(compand, resizeProcessor.Options.Compand);
    }

    [Fact]
    public void ResizeWithOptions()
    {
        int width = 50;
        int height = 100;
        IResampler sampler = KnownResamplers.Lanczos3;
        bool compand = true;
        ResizeMode mode = ResizeMode.Stretch;

        ResizeOptions resizeOptions = new()
        {
            Size = new Size(width, height),
            Sampler = sampler,
            Compand = compand,
            Mode = mode
        };

        this.operations.Resize(resizeOptions);
        ResizeProcessor resizeProcessor = this.Verify<ResizeProcessor>();

        Assert.Equal(width, resizeProcessor.DestinationWidth);
        Assert.Equal(height, resizeProcessor.DestinationHeight);
        Assert.Equal(sampler, resizeProcessor.Options.Sampler);
        Assert.Equal(compand, resizeProcessor.Options.Compand);

        // Ensure options are not altered.
        Assert.Equal(width, resizeOptions.Size.Width);
        Assert.Equal(height, resizeOptions.Size.Height);
        Assert.Equal(sampler, resizeOptions.Sampler);
        Assert.Equal(compand, resizeOptions.Compand);
        Assert.Equal(mode, resizeOptions.Mode);
    }

    [Fact]
    public void HwIntrinsics_Resize()
    {
        static void RunTest()
        {
            using Image<Rgba32> image = new(50, 50);
            image.Mutate(img => img.Resize(25, 25));

            Assert.Equal(25, image.Width);
            Assert.Equal(25, image.Height);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableFMA);
    }
}
