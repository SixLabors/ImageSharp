// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class LoadPixelData
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void FromPixels(bool useSpan)
            {
                Rgba32[] data = { Rgba32.Black, Rgba32.White, Rgba32.White, Rgba32.Black, };

                using (Image<Rgba32> img = useSpan
                                               ? Image.LoadPixelData<Rgba32>(data.AsSpan(), 2, 2)
                                               : Image.LoadPixelData<Rgba32>(data, 2, 2))
                {
                    Assert.NotNull(img);
                    Assert.Equal(Rgba32.Black, img[0, 0]);
                    Assert.Equal(Rgba32.White, img[0, 1]);

                    Assert.Equal(Rgba32.White, img[1, 0]);
                    Assert.Equal(Rgba32.Black, img[1, 1]);
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void FromBytes(bool useSpan)
            {
                byte[] data =
                    {
                        0, 0, 0, 255, // 0,0
                        255, 255, 255, 255, // 0,1
                        255, 255, 255, 255, // 1,0
                        0, 0, 0, 255, // 1,1
                    };
                using (Image<Rgba32> img = useSpan
                                               ? Image.LoadPixelData<Rgba32>(data.AsSpan(), 2, 2)
                                               : Image.LoadPixelData<Rgba32>(data, 2, 2))
                {
                    Assert.NotNull(img);
                    Assert.Equal(Rgba32.Black, img[0, 0]);
                    Assert.Equal(Rgba32.White, img[0, 1]);

                    Assert.Equal(Rgba32.White, img[1, 0]);
                    Assert.Equal(Rgba32.Black, img[1, 1]);
                }
            }
        }
    }
}