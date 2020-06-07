// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution
{
    [GroupOutput("Convolution")]
    public abstract class Basic1ParameterConvolutionTests
    {
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.05F);

        public static readonly TheoryData<int> Values = new TheoryData<int> { 3, 5 };

        public static readonly string[] InputImages =
            {
                TestImages.Bmp.Car,
                TestImages.Png.CalliphoraPartial
            };

        [Theory]
        [WithFileCollection(nameof(InputImages), nameof(Values), PixelTypes.Rgba32)]
        public void OnFullImage<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.Utility.TestGroupName = this.GetType().Name;
            provider.RunValidatingProcessorTest(
                x => this.Apply(x, value),
                value,
                ValidatorComparer);
        }

        [Theory]
        [WithFileCollection(nameof(InputImages), nameof(Values), PixelTypes.Rgba32)]
        public void InBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.Utility.TestGroupName = this.GetType().Name;
            provider.RunRectangleConstrainedValidatingProcessorTest(
                (x, rect) => this.Apply(x, value, rect),
                value,
                ValidatorComparer);
        }

        protected abstract void Apply(IImageProcessingContext ctx, int value);

        protected abstract void Apply(IImageProcessingContext ctx, int value, Rectangle bounds);
    }
}
