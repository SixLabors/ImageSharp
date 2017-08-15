// <copyright file="BackgroundColorTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Processors.Effects
{
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;
    using Xunit;

    public class BackgroundColorTest : FileTestBase
    {
        [Theory]
        [WithFileCollection(nameof(DefaultFiles), DefaultPixelType)]
        public void ImageShouldApplyBackgroundColorFilter<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.BackgroundColor(NamedColors<TPixel>.HotPink));
                image.DebugSave(provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), DefaultPixelType)]
        public void ImageShouldApplyBackgroundColorFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.BackgroundColor(NamedColors<TPixel>.HotPink, bounds));
                image.DebugSave(provider);

                PercentageImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}