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
        [Fact]
        public void BitmapCanEncodeDifferentBitRates()
        {
            const string path = "TestOutput/Bmp";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Image image = new Image(stream);
                    string encodeFilename = path + "/24-" + Path.GetFileNameWithoutExtension(file) + ".bmp";

                    using (FileStream output = File.OpenWrite(encodeFilename))
                    {
                        image.Save(output, new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Pixel24 });
                    }

                    encodeFilename = path + "/32-" + Path.GetFileNameWithoutExtension(file) + ".bmp";

                    using (FileStream output = File.OpenWrite(encodeFilename))
                    {
                        image.Save(output, new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Pixel32 });
                    }
                }
            }
        }
    }
}