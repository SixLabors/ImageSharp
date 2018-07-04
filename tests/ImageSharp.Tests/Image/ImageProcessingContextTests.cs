// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ImageProcessingContextTests
    {
        [Fact]
        public void MutatedSizeIsAccuratePerOperation()
        {
            var x500 = new Size(500, 500);
            var x400 = new Size(400, 400);
            var x300 = new Size(300, 300);
            var x200 = new Size(200, 200);
            var x100 = new Size(100, 100);
            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x =>
                    x.AssertSize(x500)
                        .Resize(x400).AssertSize(x400)
                        .Resize(x300).AssertSize(x300)
                        .Resize(x200).AssertSize(x200)
                        .Resize(x100).AssertSize(x100));
            }
        }

        [Fact]
        public void ClonedSizeIsAccuratePerOperation()
        {
            var x500 = new Size(500, 500);
            var x400 = new Size(400, 400);
            var x300 = new Size(300, 300);
            var x200 = new Size(200, 200);
            var x100 = new Size(100, 100);
            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Clone(x =>
                    x.AssertSize(x500)
                        .Resize(x400).AssertSize(x400)
                        .Resize(x300).AssertSize(x300)
                        .Resize(x200).AssertSize(x200)
                        .Resize(x100).AssertSize(x100));
            }
        }
    }

    public static class SizeAssertationExtensions
    {
        public static IImageProcessingContext<Rgba32> AssertSize(this IImageProcessingContext<Rgba32> context, Size size)
        {
            Assert.Equal(size, context.GetCurrentSize());
            return context;
        }
    }
}