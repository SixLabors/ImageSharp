// <copyright file="BitmapTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.Diagnostics;
    using System.IO;

    using Formats;

    using Xunit;

    public class BitmapTests : FileTestBase
    {
        [Fact]
        public void BitmapCanEncodeDifferentBitRates()
        {
            if (!Directory.Exists("TestOutput/Encode/Bitmap"))
            {
                Directory.CreateDirectory("TestOutput/Encode/Bitmap");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();

                    Image image = new Image(stream);
                    string encodeFilename = "TestOutput/Encode/Bitmap/" + "24-" + Path.GetFileNameWithoutExtension(file) + ".bmp";

                    using (FileStream output = File.OpenWrite(encodeFilename))
                    {
                        image.Save(output, new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Pixel24 });
                    }

                    encodeFilename = "TestOutput/Encode/Bitmap/" + "32-" + Path.GetFileNameWithoutExtension(file) + ".bmp";

                    using (FileStream output = File.OpenWrite(encodeFilename))
                    {
                        image.Save(output, new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Pixel32 });
                    }


                    Trace.WriteLine($"{file} : {watch.ElapsedMilliseconds}ms");
                }
            }
        }
    }
}