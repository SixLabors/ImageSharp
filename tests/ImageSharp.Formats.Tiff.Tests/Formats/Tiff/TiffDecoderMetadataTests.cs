// <copyright file="TiffDecoderMetadataTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using Xunit;

    using ImageSharp.Formats;
    using ImageSharp.Formats.Tiff;

    public class TiffDecoderMetadataTests
    {
        public static object[][] IsLittleEndianValues = new[] { new object[] { false },
                                                                new object[] { true } };

        [Theory]
        [InlineData(false, 150u, 1u, 200u, 1u, 2u /* Inch */, 150.0, 200.0)]
        [InlineData(false, 150u, 1u, 200u, 1u, 3u /* Cm */, 150.0 * 2.54, 200.0 * 2.54)]
        [InlineData(false, 150u, 1u, 200u, 1u, 1u /* None */, 96.0, 96.0)]
        [InlineData(false, 150u, 1u, 200u, 1u, null /* Inch */, 150.0, 200.0)]
        [InlineData(false, 5u, 2u, 9u, 4u, 2u /* Inch */, 2.5, 2.25)]
        [InlineData(false, null, null, null, null, null /* Inch */, 96.0, 96.0)]
        [InlineData(false, 150u, 1u, null, null, 2u /* Inch */, 150.0, 96.0)]
        [InlineData(false, null, null, 200u, 1u, 2u /* Inch */, 96.0, 200.0)]
        [InlineData(true, 150u, 1u, 200u, 1u, 2u /* Inch */, 150.0, 200.0)]
        [InlineData(true, 150u, 1u, 200u, 1u, 3u /* Cm */, 150.0 * 2.54, 200.0 * 2.54)]
        [InlineData(true, 150u, 1u, 200u, 1u, 1u /* None */, 96.0, 96.0)]
        [InlineData(true, 5u, 2u, 9u, 4u, 2u /* Inch */, 2.5, 2.25)]
        [InlineData(true, 150u, 1u, 200u, 1u, null /* Inch */, 150.0, 200.0)]
        [InlineData(true, null, null, null, null, null /* Inch */, 96.0, 96.0)]
        [InlineData(true, 150u, 1u, null, null, 2u /* Inch */, 150.0, 96.0)]
        [InlineData(true, null, null, 200u, 1u, 2u /* Inch */, 96.0, 200.0)]
        public void DecodeImage_SetsImageResolution(bool isLittleEndian, uint? xResolutionNumerator, uint? xResolutionDenominator,
            uint? yResolutionNumerator, uint? yResolutionDenominator, uint? resolutionUnit,
            double expectedHorizonalResolution, double expectedVerticalResolution)
        {
            TiffGenIfd ifdGen = new TiffGenIfd();

            if (xResolutionNumerator != null)
            {
                ifdGen.WithEntry(TiffGenEntry.Rational(TiffTags.XResolution, xResolutionNumerator.Value, xResolutionDenominator.Value));
            }

            if (yResolutionNumerator != null)
            {
                ifdGen.WithEntry(TiffGenEntry.Rational(TiffTags.YResolution, yResolutionNumerator.Value, yResolutionDenominator.Value));
            }

            if (resolutionUnit != null)
            {
                ifdGen.WithEntry(TiffGenEntry.Integer(TiffTags.ResolutionUnit, TiffType.Short, resolutionUnit.Value));
            }

            Stream stream = ifdGen.ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            Image<Rgba32> image = new Image<Rgba32>(null, 20, 20);

            decoder.ReadMetadata<Rgba32>(ifd, image);

            Assert.Equal(expectedHorizonalResolution, image.MetaData.HorizontalResolution, 10);
            Assert.Equal(expectedVerticalResolution, image.MetaData.VerticalResolution, 10);
        }
    }
}