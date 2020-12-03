// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class GenericBlock8x8Tests
    {
        public static Image<TPixel> CreateTestImage<TPixel>()
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var image = new Image<TPixel>(10, 10);
            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    var rgba = new Rgba32((byte)(i + 1), (byte)(j + 1), 200, 255);
                    var color = default(TPixel);
                    color.FromRgba32(rgba);

                    pixels[i, j] = color;
                }
            }

            return image;
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Rgb24 | PixelTypes.Rgba32 /* | PixelTypes.Rgba32 | PixelTypes.Argb32*/)]
        public void LoadAndStretchCorners_FromOrigo<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> s = provider.GetImage())
            {
                var d = default(GenericBlock8x8<TPixel>);
                var rowOctet = new RowOctet<TPixel>(s.GetRootFramePixelBuffer(), 0);
                d.LoadAndStretchEdges(s.Frames.RootFrame.PixelBuffer, 0, 0, rowOctet);

                TPixel a = s.Frames.RootFrame[0, 0];
                TPixel b = d[0, 0];

                Assert.Equal(s[0, 0], d[0, 0]);
                Assert.Equal(s[1, 0], d[1, 0]);
                Assert.Equal(s[7, 0], d[7, 0]);
                Assert.Equal(s[0, 1], d[0, 1]);
                Assert.Equal(s[1, 1], d[1, 1]);
                Assert.Equal(s[7, 0], d[7, 0]);
                Assert.Equal(s[0, 7], d[0, 7]);
                Assert.Equal(s[7, 7], d[7, 7]);
            }
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Rgb24 | PixelTypes.Rgba32)]
        public void LoadAndStretchCorners_WithOffset<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> s = provider.GetImage())
            {
                var d = default(GenericBlock8x8<TPixel>);
                var rowOctet = new RowOctet<TPixel>(s.GetRootFramePixelBuffer(), 7);
                d.LoadAndStretchEdges(s.Frames.RootFrame.PixelBuffer, 6, 7, rowOctet);

                Assert.Equal(s[6, 7], d[0, 0]);
                Assert.Equal(s[6, 8], d[0, 1]);
                Assert.Equal(s[7, 8], d[1, 1]);

                Assert.Equal(s[6, 9], d[0, 2]);
                Assert.Equal(s[6, 9], d[0, 3]);
                Assert.Equal(s[6, 9], d[0, 7]);

                Assert.Equal(s[7, 9], d[1, 2]);
                Assert.Equal(s[7, 9], d[1, 3]);
                Assert.Equal(s[7, 9], d[1, 7]);

                Assert.Equal(s[9, 9], d[3, 2]);
                Assert.Equal(s[9, 9], d[3, 3]);
                Assert.Equal(s[9, 9], d[3, 7]);

                Assert.Equal(s[9, 7], d[3, 0]);
                Assert.Equal(s[9, 7], d[4, 0]);
                Assert.Equal(s[9, 7], d[7, 0]);

                Assert.Equal(s[9, 9], d[3, 2]);
                Assert.Equal(s[9, 9], d[4, 2]);
                Assert.Equal(s[9, 9], d[7, 2]);

                Assert.Equal(s[9, 9], d[4, 3]);
                Assert.Equal(s[9, 9], d[7, 7]);
            }
        }

        [Fact]
        public void Indexer()
        {
            var block = default(GenericBlock8x8<Rgb24>);
            Span<Rgb24> span = block.AsSpanUnsafe();
            Assert.Equal(64, span.Length);

            for (int i = 0; i < 64; i++)
            {
                span[i] = new Rgb24((byte)i, (byte)(2 * i), (byte)(3 * i));
            }

            var expected00 = new Rgb24(0, 0, 0);
            var expected07 = new Rgb24(7, 14, 21);
            var expected11 = new Rgb24(9, 18, 27);
            var expected77 = new Rgb24(63, 126, 189);
            var expected67 = new Rgb24(62, 124, 186);

            Assert.Equal(expected00, block[0, 0]);
            Assert.Equal(expected07, block[7, 0]);
            Assert.Equal(expected11, block[1, 1]);
            Assert.Equal(expected67, block[6, 7]);
            Assert.Equal(expected77, block[7, 7]);
        }
    }
}
