// <copyright file="ColorBlindnessTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using ImageSharp.Processing;
    using System.IO;

    using ImageSharp.PixelFormats;

    using Xunit;

    public class ColorBlindnessTest : FileTestBase
    {
        public static readonly TheoryData<ColorBlindness> ColorBlindnessFilters
        = new TheoryData<ColorBlindness>
        {
            ColorBlindness.Achromatomaly,
            ColorBlindness.Achromatopsia,
            ColorBlindness.Deuteranomaly,
            ColorBlindness.Deuteranopia,
            ColorBlindness.Protanomaly,
            ColorBlindness.Protanopia,
            ColorBlindness.Tritanomaly,
            ColorBlindness.Tritanopia
        };

        [Theory]
        [MemberData(nameof(ColorBlindnessFilters))]
        public void ImageShouldApplyColorBlindnessFilter(ColorBlindness colorBlindness)
        {
            string path = this.CreateOutputDirectory("ColorBlindness");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(colorBlindness);
                using (Image<Rgba32> image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.ColorBlindness(colorBlindness).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ColorBlindnessFilters))]
        public void ImageShouldApplyBrightnessFilterInBox(ColorBlindness colorBlindness)
        {
            string path = this.CreateOutputDirectory("ColorBlindness");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(colorBlindness + "-InBox");
                using (Image<Rgba32> image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.ColorBlindness(colorBlindness, new Rectangle(10, 10, image.Width / 2, image.Height / 2)).Save(output);
                }
            }
        }
    }
}