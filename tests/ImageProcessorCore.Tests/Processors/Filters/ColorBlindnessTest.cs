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
            const string path = "TestOutput/ColorBlindness";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + colorBlindness + Path.GetExtension(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.ColorBlindness(colorBlindness)
                             .Save(output);
                    }
                }
            }
        }
    }
}