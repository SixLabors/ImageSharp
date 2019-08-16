// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Drawing
{
    [GroupOutput("Drawing")]
    public class FillImageBrushTests
    {
        [Fact]
        public void DoesNotDisposeImage()
        {
            using (var src = new Image<Rgba32>(5, 5))
            {
                var brush = new ImageBrush(src);
                using (var dest = new Image<Rgba32>(10, 10))
                {
                    dest.Mutate(c => c.Fill(brush, new Rectangle(0, 0, 10, 10)));
                    dest.Mutate(c => c.Fill(brush, new Rectangle(0, 0, 10, 10)));
                }
            }
        }
        
        [Theory]
        [WithTestPatternImages(200, 200, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void UseBrushOfDifferentPixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            byte[] data = TestFile.Create(TestImages.Png.Ducky).Bytes;
            using (Image<TPixel> background = provider.GetImage())
            using (Image overlay = provider.PixelType == PixelTypes.Rgba32
                                       ? (Image)Image.Load<Bgra32>(data)
                                       : Image.Load<Rgba32>(data))
            {
                var brush = new ImageBrush(overlay);
                background.Mutate(c => c.Fill(brush));

                background.DebugSave(provider, appendSourceFileOrDescription : false);
                background.CompareToReferenceOutput(provider, appendSourceFileOrDescription: false);
            }
        }
    }
}