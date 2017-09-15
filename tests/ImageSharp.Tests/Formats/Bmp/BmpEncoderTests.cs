// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class BmpEncoderTests : FileTestBase
    {
        public static readonly TheoryData<BmpBitsPerPixel> BitsPerPixel
        = new TheoryData<BmpBitsPerPixel>
        {
            BmpBitsPerPixel.Pixel24,
            BmpBitsPerPixel.Pixel32
        };

        [Theory]
        [MemberData(nameof(BitsPerPixel))]
        public void BitmapCanEncodeDifferentBitRates(BmpBitsPerPixel bitsPerPixel)
        {
            string path = TestEnvironment.CreateOutputDirectory("Bmp");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileNameWithoutExtension(bitsPerPixel);
                using (Image<Rgba32> image = file.CreateImage())
                {
                    image.Save($"{path}/{filename}.bmp", new BmpEncoder { BitsPerPixel = bitsPerPixel });
                }
            }
        }
    }
}