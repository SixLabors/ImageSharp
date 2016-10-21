// <copyright file="BitmapTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Formats;

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
        [MemberData("BitsPerPixel")]
        public void BitmapCanEncodeDifferentBitRates(BmpBitsPerPixel bitsPerPixel)
        {
            const string path = "TestOutput/Bmp";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileNameWithoutExtension(bitsPerPixel);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}.bmp"))
                {
                    image.Save(output, new BmpEncoder { BitsPerPixel = bitsPerPixel });
                }
            }
        }
    }
}