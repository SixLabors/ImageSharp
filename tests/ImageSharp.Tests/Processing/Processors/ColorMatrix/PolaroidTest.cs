// <copyright file="PolaroidTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Processors.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Tests.TestUtilities.ImageComparison;

    using SixLabors.Primitives;
    using Xunit;

    public class PolaroidTest : FileTestBase
    {
        [Theory]
        [WithFileCollection(nameof(DefaultFiles), DefaultPixelType)]
        public void ImageShouldApplyPolaroidFilter<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Polaroid());
                image.DebugSave(provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), DefaultPixelType)]
        public void ImageShouldApplyPolaroidFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Polaroid(bounds));
                image.DebugSave(provider);

                PercentageImageComparer_Old.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}