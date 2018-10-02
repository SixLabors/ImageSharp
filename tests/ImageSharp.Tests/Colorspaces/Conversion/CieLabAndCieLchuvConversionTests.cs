// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLab"/>-<see cref="CieLchuv"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
    /// </remarks>
    public class CieLabAndCieLchuvConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLchuv"/> to <see cref="CieLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(54.2917, 106.8391, 40.8526, 54.9205055, 30.7944126, 93.17662)]
        [InlineData(100, 0, 0, 100, 0, 0)]
        [InlineData(100, 50, 180, 99.74778, -35.5287476, -4.24233675)]
        [InlineData(10, 36.0555, 56.3099, 10.2056971, 7.886916, 17.498457)]
        [InlineData(10, 36.0555, 123.6901, 9.953703, -35.1176033, 16.8696461)]
        [InlineData(10, 36.0555, 303.6901, 9.805839, 55.69225, -36.6074753)]
        [InlineData(10, 36.0555, 236.3099, 8.86916, -34.4068336, -42.2136269)]
        public void Convert_Lchuv_to_Lab(float l, float c, float h, float l2, float a, float b)
        {
            // Arrange
            var input = new CieLchuv(l, c, h);
            var expected = new CieLab(l2, a, b);

            Span<CieLchuv> inputSpan = new CieLchuv[5];
            inputSpan.Fill(input);

            Span<CieLab> actualSpan = new CieLab[5];

            // Act
            var actual = Converter.ToCieLab(input);
            Converter.Convert(inputSpan, actualSpan, actualSpan.Length);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="CieLab"/> to <see cref="CieLchuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(54.9205055, 30.7944126, 93.17662, 54.9205055, 103.269287, 35.46892)]
        [InlineData(100, 0, 0, 100, 29.5789261, 60.1635857)]
        [InlineData(99.74778, -35.5287476, -4.24233675, 99.74778, 48.8177834, 139.54837)]
        [InlineData(10.2056971, 7.886916, 17.498457, 10.205699, 17.00984, 42.9908066)]
        [InlineData(9.953703, -35.1176033, 16.8696461, 9.953705, 25.3788586, 141.070892)]
        [InlineData(9.805839, 55.69225, -36.6074753, 9.80584049, 35.3214073, 314.4875)]
        [InlineData(8.86916, -34.4068336, -42.2136269, 8.869162, 32.1432457, 227.960419)]

        public void Convert_Lab_to_Lchuv(float l, float a, float b, float l2, float c, float h)
        {
            // Arrange
            var input = new CieLab(l, a, b);
            var expected = new CieLchuv(l2, c, h);

            Span<CieLab> inputSpan = new CieLab[5];
            inputSpan.Fill(input);

            Span<CieLchuv> actualSpan = new CieLchuv[5];

            // Act
            var actual = Converter.ToCieLchuv(input);
            Converter.Convert(inputSpan, actualSpan, actualSpan.Length);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }
    }
}