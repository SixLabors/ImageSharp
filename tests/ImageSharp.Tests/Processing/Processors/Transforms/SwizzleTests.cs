// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Extensions.Transforms;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    [GroupOutput("Transforms")]
    public class SwizzleTests
    {
        private struct InvertXAndYSwizzler : ISwizzler
        {
            public InvertXAndYSwizzler(Size sourceSize)
            {
                this.DestinationSize = new Size(sourceSize.Height, sourceSize.Width);
            }

            public Size DestinationSize { get; }

            public void Transform(Point point, out Point newPoint)
                => newPoint = new Point(point.Y, point.X);
        }

        [Theory]
        [WithTestPatternImages(20, 37, PixelTypes.Rgba32)]
        [WithTestPatternImages(53, 37, PixelTypes.Byte4)]
        [WithTestPatternImages(17, 32, PixelTypes.Rgba32)]
        public void InvertXAndYSwizzle<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Image<TPixel> image = provider.GetImage();
            provider.RunValidatingProcessorTest(
                ctx => ctx.Swizzle(new InvertXAndYSwizzler(new Size(image.Width, image.Height))),
                testOutputDetails: nameof(InvertXAndYSwizzler),
                appendPixelTypeToFileName: false);
        }
    }
}
