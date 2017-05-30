// <copyright file="SepiaTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using ImageSharp.PixelFormats;

    using Xunit;

    public class SepiaTest : FileTestBase
    {
        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), StandardPixelType)]
        public void ImageShouldApplySepiaFilter<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Sepia()
                    .DebugSave(provider, null, Extensions.Bmp);
            }
        }

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), StandardPixelType)]
        public void ImageShouldApplySepiaFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = new Image<TPixel>(source))
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Sepia(bounds)
                    .DebugSave(provider, null, Extensions.Bmp);

                // Draw identical shapes over the bounded and compare to ensure changes are constrained.
                image.Fill(NamedColors<TPixel>.HotPink, bounds);
                source.Fill(NamedColors<TPixel>.HotPink, bounds);
                ImageComparer.CheckSimilarity(image, source);
            }
        }
    }
}