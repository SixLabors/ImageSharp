// <copyright file="OilPaintTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System;
    using System.IO;

    using Xunit;

    public class OilPaintTest : FileTestBase
    {
        public static readonly TheoryData<Tuple<int, int>> OilPaintValues
            = new TheoryData<Tuple<int, int>>
            {
                new Tuple<int, int>(15,10),
                new Tuple<int, int>(6,5)
            };

        [Theory]
        [MemberData(nameof(OilPaintValues))]
        public void ImageShouldApplyOilPaintFilter(Tuple<int, int> value)
        {
            const string path = "TestOutput/OilPaint";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + value + Path.GetExtension(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.OilPaint(value.Item1, value.Item2)
                             .Save(output);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(OilPaintValues))]
        public void ImageShouldApplyOilPaintFilterInBox(Tuple<int, int> value)
        {
            const string path = "TestOutput/OilPaint";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + value + "-InBox" + Path.GetExtension(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.OilPaint(value.Item1, value.Item2, new Rectangle(10, 10, image.Width / 2, image.Height / 2))
                             .Save(output);
                    }
                }
            }
        }
    }
}