// <copyright file="LomographTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Processors.ColorMatrix
{
    using System.IO;

    using ImageSharp.PixelFormats;
    using ImageSharp.Tests.TestUtilities.ImageComparison;

    using SixLabors.Primitives;
    using Xunit;

    public class LomographTest : FileTestBase
    {
        [Theory]
        [WithFileCollection(nameof(DefaultFiles), DefaultPixelType)]
        public void ImageShouldApplyLomographFilter<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Lomograph());
                image.DebugSave(provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), DefaultPixelType)]
        public void ImageShouldApplyLomographFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Lomograph(bounds));
                image.DebugSave(provider);

                PercentageImageComparer_Old.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}