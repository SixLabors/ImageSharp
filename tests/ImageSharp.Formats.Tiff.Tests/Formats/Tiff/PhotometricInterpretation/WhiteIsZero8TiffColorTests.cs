// <copyright file="WhiteIsZero8TiffColorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.Collections.Generic;
    using Xunit;

    using ImageSharp.Formats;

    public class WhiteIsZero8TiffColorTests : PhotometricInterpretationTestBase
    {
        private static Color Gray000 = new Color(255, 255, 255, 255);
        private static Color Gray128 = new Color(127, 127, 127, 255);
        private static Color Gray255 = new Color(0, 0, 0, 255);

        private static byte[] GrayscaleBytes4x4 = new byte[] { 128, 255, 000, 255,
                                                               255, 255, 255, 255,
                                                               000, 128, 128, 255,
                                                               255, 000, 255, 128 };

        private static Color[][] GrayscaleResult4x4 = new[] { new[] { Gray128, Gray255, Gray000, Gray255 },
                                                              new[] { Gray255, Gray255, Gray255, Gray255 },
                                                              new[] { Gray000, Gray128, Gray128, Gray255 },
                                                              new[] { Gray255, Gray000, Gray255, Gray128 }};

        public static IEnumerable<object[]> DecodeData
        {
            get
            {
                yield return new object[] { GrayscaleBytes4x4, 0, 0, 4, 4, GrayscaleResult4x4 };
                yield return new object[] { GrayscaleBytes4x4, 0, 0, 4, 4, Offset(GrayscaleResult4x4, 0, 0, 6, 6) };
                yield return new object[] { GrayscaleBytes4x4, 1, 0, 4, 4, Offset(GrayscaleResult4x4, 1, 0, 6, 6) };
                yield return new object[] { GrayscaleBytes4x4, 0, 1, 4, 4, Offset(GrayscaleResult4x4, 0, 1, 6, 6) };
                yield return new object[] { GrayscaleBytes4x4, 1, 1, 4, 4, Offset(GrayscaleResult4x4, 1, 1, 6, 6) };
            }
        }

        [Theory]
        [MemberData(nameof(DecodeData))]
        public void Decode_WritesPixelData(byte[] inputData, int left, int top, int width, int height, Color[][] expectedResult)
        {
            AssertDecode(expectedResult, pixels =>
                {
                    WhiteIsZero8TiffColor.Decode(inputData, pixels, left, top, width, height);
                });
        }
    }
}