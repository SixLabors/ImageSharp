// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    [GroupOutput("Transforms")]
    public class FlipTests
    {
        public static readonly TheoryData<FlipMode> FlipValues =
            new TheoryData<FlipMode>
                {
                    FlipMode.None,
                    FlipMode.Vertical,
                    FlipMode.Horizontal,
                };

        [Theory]
        [WithTestPatternImages(nameof(FlipValues), 20, 37, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(FlipValues), 53, 37, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(FlipValues), 17, 32, PixelTypes.Rgba32)]
        public void Flip<TPixel>(TestImageProvider<TPixel> provider, FlipMode flipMode)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(
                ctx => ctx.Flip(flipMode),
                testOutputDetails: flipMode,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithTestPatternImages(nameof(FlipValues), 53, 37, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(FlipValues), 17, 32, PixelTypes.Rgba32)]
        public void Flip_WorksOnWrappedMemoryImage<TPixel>(TestImageProvider<TPixel> provider, FlipMode flipMode)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTestOnWrappedMemoryImage(
                ctx => ctx.Flip(flipMode),
                testOutputDetails: flipMode,
                useReferenceOutputFrom: nameof(this.Flip),
                appendPixelTypeToFileName: false);
        }
    }
}
