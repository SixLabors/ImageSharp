﻿// <copyright file="PolaroidTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using ImageSharp.PixelFormats;

    using Xunit;

    public class PolaroidTest : FileTestBase
    {
        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), StandardPixelTypes)]
        public void ImageShouldApplyPolaroidFilter<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Polaroid()
                    .DebugSave(provider, null, Extensions.Bmp);
            }
        }

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), StandardPixelTypes)]
        public void ImageShouldApplyPolaroidFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = new Image<TPixel>(source))
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Polaroid(bounds)
                    .DebugSave(provider, null, Extensions.Bmp);

                // Draw identical shapes over the bounded and compare to ensure changes are constrained.
                image.Fill(NamedColors<TPixel>.HotPink, bounds);
                source.Fill(NamedColors<TPixel>.HotPink, bounds);
                ImageComparer.CheckSimilarity(image, source);
            }
        }
    }
}