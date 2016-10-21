// <copyright file="ColorBlindnessTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

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
        [MemberData("ColorBlindnessFilters")]
        public void ImageShouldApplyColorBlindnessFilter(ColorBlindness colorBlindness)
        {
            string path = CreateOutputDirectory("ColorBlindness");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(colorBlindness);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.ColorBlindness(colorBlindness)
                          .Save(output);
                }
            }
        }
    }
}