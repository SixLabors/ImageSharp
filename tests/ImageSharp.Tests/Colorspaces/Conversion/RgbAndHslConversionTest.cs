// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Rgb"/>-<see cref="Hsl"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.colorhexa.com"/>
    /// <see href="http://www.rapidtables.com/convert/color/hsl-to-rgb"/>
    /// </remarks>
    public class RgbAndHslConversionTest
    {
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);

        public static TheoryData<float, float, float, float, float, float> Hsl_To_Rgb
        = new TheoryData<float, float, float, float, float, float>
        {
            { 0, 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 1, 1 },
            { 360, 1, 1, 1, 1, 1 },
            { 0, 1, .5F, 1, 0, 0 },
            { 120, 1, .5F, 0, 1, 0 },
            { 240, 1, .5F, 0, 0, 1 }
        };

        private readonly ITestOutputHelper output;

        public RgbAndHslConversionTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Tests conversion from <see cref="Hsl"/> to <see cref="Rgb"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0, 1, 1, 1, 1, 1)]
        [InlineData(360, 1, 1, 1, 1, 1)]
        [InlineData(0, 1, .5F, 1, 0, 0)]
        [InlineData(120, 1, .5F, 0, 1, 0)]
        [InlineData(240, 1, .5F, 0, 0, 1)]
        //[Theory]
        //[MemberData(nameof(Hsl_To_Rgb))]
        public void Convert_Hsl_To_Rgb(float h, float s, float l, float r, float g, float b)
        {
            // Arrange
            var input = new Hsl(h, s, l);
            var expected = new Rgb(r, g, b);

            Span<Hsl> inputSpan = new Hsl[5];
            inputSpan.Fill(input);

            Span<Rgb> actualSpan = new Rgb[5];

            // Act
            var actual = Converter.ToRgb(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(Rgb.DefaultWorkingSpace, actual.WorkingSpace, ColorSpaceComparer);
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }

            this.output.WriteLine("Verifying Convert_Hsl_To_Rgb is run");
        }

        /// <summary>
        /// Tests conversion from <see cref="Rgb"/> to <see cref="Hsl"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1, 1, 1, 0, 0, 1)]
        [InlineData(1, 0, 0, 0, 1, .5F)]
        [InlineData(0, 1, 0, 120, 1, .5F)]
        [InlineData(0, 0, 1, 240, 1, .5F)]
        [InlineData(0.7, 0.8, 0.6, 90, 0.3333, 0.7F)]
        public void Convert_Rgb_To_Hsl(float r, float g, float b, float h, float s, float l)
        {
            // Arrange
            var input = new Rgb(r, g, b);
            var expected = new Hsl(h, s, l);


            Span<Rgb> inputSpan = new Rgb[5];
            inputSpan.Fill(input);

            Span<Hsl> actualSpan = new Hsl[5];

            // Act
            var actual = Converter.ToHsl(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }
    }
}
