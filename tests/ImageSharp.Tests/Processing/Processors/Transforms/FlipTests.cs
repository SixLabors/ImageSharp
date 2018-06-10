// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    using SixLabors.ImageSharp.Processing.Transforms;

    public class FlipTests : FileTestBase
    {
        public static readonly string[] FlipFiles = { TestImages.Bmp.F };

        public static readonly TheoryData<FlipMode> FlipValues
            = new TheoryData<FlipMode>
        {
            { FlipMode.None },
            { FlipMode.Vertical },
            { FlipMode.Horizontal },
        };

        [Theory]
        [WithTestPatternImages(nameof(FlipValues), 53, 37, DefaultPixelType)]
        [WithTestPatternImages(nameof(FlipValues), 17, 32, DefaultPixelType)]
        public void Flip<TPixel>(TestImageProvider<TPixel> provider, FlipMode flipMode)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(ctx => ctx.Flip(flipMode), testOutputDetails: flipMode);
        }

        [Theory]
        [WithTestPatternImages(nameof(FlipValues), 53, 37, DefaultPixelType)]
        [WithTestPatternImages(nameof(FlipValues), 17, 32, DefaultPixelType)]
        public void Flip_WorksOnWrappedMemoryImage<TPixel>(TestImageProvider<TPixel> provider, FlipMode flipMode)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTestOnWrappedMemoryImage(ctx => ctx.Flip(flipMode), testOutputDetails: flipMode);
        }
    }
}