// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Reflection;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.Primitives;

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
            where TPixel : struct, IPixel<TPixel>
        {
            provider.Utility.TestGroupName = this.GetType().Name;
            var f = (FieldInfo)typeof(NamedColors<TPixel>).GetMember(colorName)[0];
            TPixel color = (TPixel)f.GetValue(null);

            provider.RunValidatingProcessorTest(x => this.Apply(x, color), colorName, ValidatorComparer, appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFileCollection(nameof(InputImages), PixelTypes.Rgba32)]
        public void FullImage_ApplyRadius<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
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
            where TPixel : struct, IPixel<TPixel>
        {
            provider.Utility.TestGroupName = this.GetType().Name;
            provider.RunRectangleConstrainedValidatingProcessorTest((x, rect) => this.Apply(x, rect));
        }

        protected abstract void Apply<T>(IImageProcessingContext<T> ctx, T color)
            where T : struct, IPixel<T>;
        
        protected abstract void Apply<T>(IImageProcessingContext<T> ctx, float radiusX, float radiusY)
            where T : struct, IPixel<T>;
        
        protected abstract void Apply<T>(IImageProcessingContext<T> ctx, Rectangle rect)
            where T : struct, IPixel<T>;
    }
}