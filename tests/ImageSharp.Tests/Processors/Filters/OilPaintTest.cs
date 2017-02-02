// <copyright file="OilPaintTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;

    using Xunit;

    public class OilPaintTest : FileTestBase
    {
        public static readonly TheoryData<Tuple<int, int>> OilPaintValues
            = new TheoryData<Tuple<int, int>>
            {
                new Tuple<int, int>(15, 10),
                new Tuple<int, int>(6, 5)
            };

        [Theory]
        [MemberData(nameof(OilPaintValues))]
        public void ImageShouldApplyOilPaintFilter(Tuple<int, int> value)
        {
            string path = this.CreateOutputDirectory("OilPaint");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    if (image.Width > value.Item2 && image.Height > value.Item2)
                    {
                        image.OilPaint(value.Item1, value.Item2).Save(output);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(OilPaintValues))]
        public void ImageShouldApplyOilPaintFilterInBox(Tuple<int, int> value)
        {
            string path = this.CreateOutputDirectory("OilPaint");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value + "-InBox");
                using (Image image = file.CreateImage())
                {
                    if (image.Width > value.Item2 && image.Height > value.Item2)
                    {
                        using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                        {
                            image.OilPaint(value.Item1, value.Item2, new Rectangle(image.Width / 4, image.Width / 4, image.Width / 2, image.Height / 2)).Save(output);
                        }
                    }
                }
            }
        }
    }
}