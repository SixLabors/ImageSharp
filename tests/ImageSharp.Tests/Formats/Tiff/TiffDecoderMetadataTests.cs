// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class TiffDecoderMetadataTests
    {
        public static object[][] BaselineMetadataValues = new[] { new object[] { false, TiffTags.Artist, TiffMetadataNames.Artist, "My Artist Name" },
                                                                  new object[] { false, TiffTags.Copyright, TiffMetadataNames.Copyright, "My Copyright Statement" },
                                                                  new object[] { false, TiffTags.DateTime, TiffMetadataNames.DateTime, "My DateTime Value" },
                                                                  new object[] { false, TiffTags.HostComputer, TiffMetadataNames.HostComputer, "My Host Computer Name" },
                                                                  new object[] { false, TiffTags.ImageDescription, TiffMetadataNames.ImageDescription, "My Image Description" },
                                                                  new object[] { false, TiffTags.Make, TiffMetadataNames.Make, "My Camera Make" },
                                                                  new object[] { false, TiffTags.Model, TiffMetadataNames.Model, "My Camera Model" },
                                                                  new object[] { false, TiffTags.Software, TiffMetadataNames.Software, "My Imaging Software" },
                                                                  new object[] { true, TiffTags.Artist, TiffMetadataNames.Artist, "My Artist Name" },
                                                                  new object[] { true, TiffTags.Copyright, TiffMetadataNames.Copyright, "My Copyright Statement" },
                                                                  new object[] { true, TiffTags.DateTime, TiffMetadataNames.DateTime, "My DateTime Value" },
                                                                  new object[] { true, TiffTags.HostComputer, TiffMetadataNames.HostComputer, "My Host Computer Name" },
                                                                  new object[] { true, TiffTags.ImageDescription, TiffMetadataNames.ImageDescription, "My Image Description" },
                                                                  new object[] { true, TiffTags.Make, TiffMetadataNames.Make, "My Camera Make" },
                                                                  new object[] { true, TiffTags.Model, TiffMetadataNames.Model, "My Camera Model" },
                                                                  new object[] { true, TiffTags.Software, TiffMetadataNames.Software, "My Imaging Software" }};

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
        public void ReadMetadata_SetsImageResolution(bool isLittleEndian, uint? xResolutionNumerator, uint? xResolutionDenominator,
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

            Assert.Equal(expectedHorizonalResolution, image.Metadata.HorizontalResolution, 10);
            Assert.Equal(expectedVerticalResolution, image.Metadata.VerticalResolution, 10);
        }

        [Theory]
        [MemberData(nameof(BaselineMetadataValues))]
        public void ReadMetadata_SetsAsciiMetadata(bool isLittleEndian, ushort tag, string metadataName, string metadataValue)
        {
            Stream stream = new TiffGenIfd()
            {
                Entries =
                                {
                                    TiffGenEntry.Integer(TiffTags.ImageWidth, TiffType.Long, 150),
                                    TiffGenEntry.Integer(TiffTags.ImageLength, TiffType.Long, 210),
                                    TiffGenEntry.Ascii(tag, metadataValue),
                                    TiffGenEntry.Integer(TiffTags.Orientation, TiffType.Short, 1)
                                }
            }
                .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            Image<Rgba32> image = new Image<Rgba32>(null, 20, 20);

            decoder.ReadMetadata<Rgba32>(ifd, image);

            TiffMetaData tiffMetadata = image.Metadata.GetFormatMetadata(TiffFormat.Instance);
            var metadata = tiffMetadata.TextTags.FirstOrDefault(m => m.Name == metadataName).Value;

            Assert.Equal(metadataValue, metadata);
        }

        [Theory]
        [MemberData(nameof(BaselineMetadataValues))]
        public void ReadMetadata_DoesntSetMetadataIfIgnoring(bool isLittleEndian, ushort tag, string metadataName, string metadataValue)
        {
            Stream stream = new TiffGenIfd()
            {
                Entries =
                                {
                                    TiffGenEntry.Integer(TiffTags.ImageWidth, TiffType.Long, 150),
                                    TiffGenEntry.Integer(TiffTags.ImageLength, TiffType.Long, 210),
                                    TiffGenEntry.Ascii(tag, metadataValue),
                                    TiffGenEntry.Integer(TiffTags.Orientation, TiffType.Short, 1)
                                }
            }
                .ToStream(isLittleEndian);

            TiffDecoder options = new TiffDecoder() { IgnoreMetadata = true };
            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, options);
            TiffIfd ifd = decoder.ReadIfd(0);
            Image<Rgba32> image = new Image<Rgba32>(null, 20, 20);

            decoder.ReadMetadata<Rgba32>(ifd, image);

            TiffMetaData tiffMetadata = image.Metadata.GetFormatMetadata(TiffFormat.Instance);
            var metadata = tiffMetadata.TextTags.FirstOrDefault(m => m.Name == metadataName).Value;

            Assert.Null(metadata);
        }
    }
}
