// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Overlays
{
    [Trait("Category", "Processors")]
    [GroupOutput("Overlays")]
    public abstract class OverlayTestBase
    {
        public static string[] ColorNames = { "Blue", "White" };

        public static string[] InputImages = { TestImages.Png.Ducky, TestImages.Png.Splash };

        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.05f);

        // [Theory]
        // [WithFileCollection(nameof(InputImages), nameof(ColorNames), PixelTypes.Rgba32)]
        // public void FullImage_ApplyColor<TPixel>(TestImageProvider<TPixel> provider, string colorName)
        //     where TPixel : unmanaged, IPixel<TPixel>
        // {
        //     provider.Utility.TestGroupName = this.GetType().Name;
        //     Color color = TestUtils.GetColorByName(colorName);
        //
        //     provider.RunValidatingProcessorTest(x => this.Apply(x, color), colorName, ValidatorComparer, appendPixelTypeToFileName: false);
        // }
        //
        // [Theory]
        // [WithFileCollection(nameof(InputImages), PixelTypes.Rgba32)]
        // public void FullImage_ApplyRadius<TPixel>(TestImageProvider<TPixel> provider)
        //     where TPixel : unmanaged, IPixel<TPixel>
        // {
        //     provider.Utility.TestGroupName = this.GetType().Name;
        //     provider.RunValidatingProcessorTest(
        //         x =>
        //             {
        //                 Size size = x.GetCurrentSize();
        //                 this.Apply(x, size.Width / 4f, size.Height / 4f);
        //             },
        //         comparer: ValidatorComparer,
        //         appendPixelTypeToFileName: false);
        // }

        // [Theory]
        // [WithFileCollection(nameof(InputImages), PixelTypes.Rgba32)]
        // public void InBox<TPixel>(TestImageProvider<TPixel> provider)
        //     where TPixel : unmanaged, IPixel<TPixel>
        // {
        //     provider.Utility.TestGroupName = this.GetType().Name;
        //     provider.RunRectangleConstrainedValidatingProcessorTest(this.Apply);
        // }

        public static TheoryData<int> Replicas = new() { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99 };

        [Theory]
        [WithTestPatternImages(nameof(Replicas), 70, 120, PixelTypes.Rgba32)]
        public void WorksWithDiscoBuffers<TPixel>(TestImageProvider<TPixel> provider, int dummy)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Assert.Equal(dummy * 2, dummy + dummy);
            provider.RunBufferCapacityLimitProcessorTest(37, c => this.Apply(c, Color.DarkRed));
        }

        protected abstract void Apply(IImageProcessingContext ctx, Color color);

        protected abstract void Apply(IImageProcessingContext ctx, float radiusX, float radiusY);

        protected abstract void Apply(IImageProcessingContext ctx, Rectangle rect);
    }
}
