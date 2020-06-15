// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Overlays
{
    [GroupOutput("Overlays")]
    public abstract class OverlayTestBase
    {
        public static string[] ColorNames = { "Blue", "White" };

        public static string[] InputImages = { TestImages.Png.Ducky, TestImages.Png.Splash };

        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.05f);

        [Theory]
        [WithFileCollection(nameof(InputImages), nameof(ColorNames), PixelTypes.Rgba32)]
        public void FullImage_ApplyColor<TPixel>(TestImageProvider<TPixel> provider, string colorName)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.Utility.TestGroupName = this.GetType().Name;
            Color color = TestUtils.GetColorByName(colorName);

            provider.RunValidatingProcessorTest(x => this.Apply(x, color), colorName, ValidatorComparer, appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFileCollection(nameof(InputImages), PixelTypes.Rgba32)]
        public void FullImage_ApplyRadius<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.Utility.TestGroupName = this.GetType().Name;
            provider.RunValidatingProcessorTest(
                x =>
                    {
                        Size size = x.GetCurrentSize();
                        this.Apply(x, size.Width / 4f, size.Height / 4f);
                    },
                comparer: ValidatorComparer,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFileCollection(nameof(InputImages), PixelTypes.Rgba32)]
        public void InBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.Utility.TestGroupName = this.GetType().Name;
            provider.RunRectangleConstrainedValidatingProcessorTest(this.Apply);
        }

        [Theory]
        [WithTestPatternImages(70, 120, PixelTypes.Rgba32)]
        public void WorksWithDiscoBuffers<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunBufferCapacityLimitProcessorTest(37, c => this.Apply(c, Color.DarkRed));
        }

        protected abstract void Apply(IImageProcessingContext ctx, Color color);

        protected abstract void Apply(IImageProcessingContext ctx, float radiusX, float radiusY);

        protected abstract void Apply(IImageProcessingContext ctx, Rectangle rect);
    }
}
