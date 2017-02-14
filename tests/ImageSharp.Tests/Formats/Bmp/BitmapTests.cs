// <copyright file="BitmapTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using ImageSharp.Formats;

namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;

    public class BitmapTests : FileTestBase
    {
        public static readonly TheoryData<BmpBitsPerPixel> BitsPerPixel
        = new TheoryData<BmpBitsPerPixel>
        {
            BmpBitsPerPixel.Pixel24 ,
            BmpBitsPerPixel.Pixel32
        };

        [Theory]
        [MemberData(nameof(BitsPerPixel))]
        public void BitmapCanEncodeDifferentBitRates(BmpBitsPerPixel bitsPerPixel)
        {
            string path = this.CreateOutputDirectory("Bmp");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileNameWithoutExtension(bitsPerPixel);
                using (Image image = file.CreateImage())
                {
                    image.Save($"{path}/{filename}.bmp", new BmpEncoder { BitsPerPixel = bitsPerPixel });
                }
            }
        }
    }
}