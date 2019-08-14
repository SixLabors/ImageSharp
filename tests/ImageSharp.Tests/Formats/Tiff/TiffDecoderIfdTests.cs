// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Tiff;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class TiffDecoderIfdTests
    {
        public static object[][] IsLittleEndianValues = new[] { new object[] { false },
                                                                new object[] { true } };

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadIfd_ReadsNextIfdOffset_IfPresent(bool isLittleEndian)
        {
            Stream stream = new TiffGenIfd()
            {
                Entries =
                                {
                                    TiffGenEntry.Integer(TiffTagId.ImageWidth, TiffTagType.Long, 150)
                                },
                NextIfd = new TiffGenIfd()
            }
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            Assert.Equal(18u, ifd.NextIfdOffset);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadIfd_ReadsNextIfdOffset_ZeroIfLastIfd(bool isLittleEndian)
        {
            Stream stream = new TiffGenIfd()
            {
                Entries =
                                {
                                    TiffGenEntry.Integer(TiffTagId.ImageWidth, TiffTagType.Long, 150)
                                }
            }
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            Assert.Equal(0u, ifd.NextIfdOffset);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadIfd_ReturnsCorrectNumberOfEntries(bool isLittleEndian)
        {
            Stream stream = new TiffGenIfd()
            {
                Entries =
                                {
                                    TiffGenEntry.Integer(TiffTagId.ImageWidth, TiffTagType.Long, 150),
                                    TiffGenEntry.Integer(TiffTagId.ImageLength, TiffTagType.Long, 210),
                                    TiffGenEntry.Integer(TiffTagId.Orientation, TiffTagType.Short, 1),
                                    TiffGenEntry.Ascii(TiffTagId.Artist, "Image Artist Name"),
                                    TiffGenEntry.Ascii(TiffTagId.HostComputer, "Host Computer Name")
                                },
                NextIfd = new TiffGenIfd()
            }
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            Assert.NotNull(ifd.Entries);
            Assert.Equal(5, ifd.Entries.Length);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadIfd_ReadsRawTiffEntryData(bool isLittleEndian)
        {
            Stream stream = new TiffGenIfd()
            {
                Entries =
                                {
                                    TiffGenEntry.Integer(TiffTagId.ImageWidth, TiffTagType.Long, 150),
                                    TiffGenEntry.Integer(TiffTagId.ImageLength, TiffTagType.Long, 210),
                                    TiffGenEntry.Integer(TiffTagId.Orientation, TiffTagType.Short, 1)
                                },
                NextIfd = new TiffGenIfd()
            }
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            TiffTag entry = ifd.Entries[1];

            byte[] expectedData = isLittleEndian ? new byte[] { 210, 0, 0, 0 } : new byte[] { 0, 0, 0, 210 };

            Assert.Equal(TiffTagId.ImageLength, entry.Tag);
            Assert.Equal(TiffTagType.Long, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(expectedData, entry.Value);
        }
    }
}