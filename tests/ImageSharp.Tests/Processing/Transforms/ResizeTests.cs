// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class ResizeTests : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void ResizeWidthAndHeight()
        {
            int width = 50;
            int height = 100;
            this.operations.Resize(width, height);
            ResizeProcessor<Rgba32> resizeProcessor = this.Verify<ResizeProcessor<Rgba32>>();

            Assert.Equal(width, resizeProcessor.Width);
            Assert.Equal(height, resizeProcessor.Height);
        }

        [Fact]
        public void ResizeWidthAndHeightAndSampler()
        {
            int width = 50;
            int height = 100;
            IResampler sampler = KnownResamplers.Lanczos3;
            this.operations.Resize(width, height, sampler);
            ResizeProcessor<Rgba32> resizeProcessor = this.Verify<ResizeProcessor<Rgba32>>();

            Assert.Equal(width, resizeProcessor.Width);
            Assert.Equal(height, resizeProcessor.Height);
            Assert.Equal(sampler, resizeProcessor.Sampler);
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
            ResizeProcessor<Rgba32> resizeProcessor = this.Verify<ResizeProcessor<Rgba32>>();

            Assert.Equal(width, resizeProcessor.Width);
            Assert.Equal(height, resizeProcessor.Height);
            Assert.Equal(sampler, resizeProcessor.Sampler);
            Assert.Equal(compand, resizeProcessor.Compand);
        }

        [Fact]
        public void ResizeWithOptions()
        {
            int width = 50;
            int height = 100;
            IResampler sampler = KnownResamplers.Lanczos3;
            bool compand = true;
            ResizeMode mode = ResizeMode.Stretch;

            var resizeOptions = new ResizeOptions
            {
                Size = new Size(width, height),
                Sampler = sampler,
                Compand = compand,
                Mode = mode
            };

            this.operations.Resize(resizeOptions);
            ResizeProcessor<Rgba32> resizeProcessor = this.Verify<ResizeProcessor<Rgba32>>();

            Assert.Equal(width, resizeProcessor.Width);
            Assert.Equal(height, resizeProcessor.Height);
            Assert.Equal(sampler, resizeProcessor.Sampler);
            Assert.Equal(compand, resizeProcessor.Compand);

            // Ensure options are not altered.
            Assert.Equal(width, resizeOptions.Size.Width);
            Assert.Equal(height, resizeOptions.Size.Height);
            Assert.Equal(sampler, resizeOptions.Sampler);
            Assert.Equal(compand, resizeOptions.Compand);
            Assert.Equal(mode, resizeOptions.Mode);
        }
    }
}