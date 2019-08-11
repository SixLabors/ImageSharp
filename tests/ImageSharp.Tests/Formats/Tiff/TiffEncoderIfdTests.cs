// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Formats.Tiff;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class TiffEncoderIfdTests
    {
        [Fact]
        public void WriteIfd_DataIsCorrectLength()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>()
                {
                    new TiffIfdEntry(TiffTags.ImageWidth, TiffType.Long, 1, new byte[] { 1, 2, 3, 4 }),
                    new TiffIfdEntry(TiffTags.ImageLength, TiffType.Long, 1, new byte[] { 5, 6, 7, 8 }),
                    new TiffIfdEntry(TiffTags.Compression, TiffType.Long, 1, new byte[] { 9, 10, 11, 12 })
                };

            using (TiffWriter writer = new TiffWriter(stream))
            {
                long nextIfdMarker = encoder.WriteIfd(writer, entries);
            }

            Assert.Equal(2 + 12 * 3 + 4, stream.Length);
        }

        [Fact]
        public void WriteIfd_WritesNumberOfEntries()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>()
                {
                    new TiffIfdEntry(TiffTags.ImageWidth, TiffType.Long, 1, new byte[] { 1, 2, 3, 4 }),
                    new TiffIfdEntry(TiffTags.ImageLength, TiffType.Long, 1, new byte[] { 5, 6, 7, 8 }),
                    new TiffIfdEntry(TiffTags.Compression, TiffType.Long, 1, new byte[] { 9, 10, 11, 12 })
                };

            using (TiffWriter writer = new TiffWriter(stream))
            {
                long nextIfdMarker = encoder.WriteIfd(writer, entries);
            }

            var ifdEntryBytes = stream.ToArray().Take(2).ToArray();
            Assert.Equal(new byte[] { 3, 0 }, ifdEntryBytes);
        }

        [Fact]
        public void WriteIfd_ReturnsNextIfdMarker()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>()
                {
                    new TiffIfdEntry(TiffTags.ImageWidth, TiffType.Long, 1, new byte[] { 1, 2, 3, 4 }),
                    new TiffIfdEntry(TiffTags.ImageLength, TiffType.Long, 1, new byte[] { 5, 6, 7, 8 }),
                    new TiffIfdEntry(TiffTags.Compression, TiffType.Long, 1, new byte[] { 9, 10, 11, 12 })
                };

            using (TiffWriter writer = new TiffWriter(stream))
            {
                long nextIfdMarker = encoder.WriteIfd(writer, entries);
                Assert.Equal(2 + 12 * 3, nextIfdMarker);
            }
        }

        [Fact]
        public void WriteIfd_WritesTagIdForEachEntry()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>()
                {
                    new TiffIfdEntry(10, TiffType.Long, 1, new byte[] { 1, 2, 3, 4 }),
                    new TiffIfdEntry(20, TiffType.Long, 1, new byte[] { 5, 6, 7, 8 }),
                    new TiffIfdEntry(30, TiffType.Long, 1, new byte[] { 9, 10, 11, 12 })
                };

            using (TiffWriter writer = new TiffWriter(stream))
            {
                long nextIfdMarker = encoder.WriteIfd(writer, entries);
            }

            var ifdEntry1Bytes = stream.ToArray().Skip(2 + 12 * 0).Take(2).ToArray();
            var ifdEntry2Bytes = stream.ToArray().Skip(2 + 12 * 1).Take(2).ToArray();
            var ifdEntry3Bytes = stream.ToArray().Skip(2 + 12 * 2).Take(2).ToArray();

            Assert.Equal(new byte[] { 10, 0 }, ifdEntry1Bytes);
            Assert.Equal(new byte[] { 20, 0 }, ifdEntry2Bytes);
            Assert.Equal(new byte[] { 30, 0 }, ifdEntry3Bytes);
        }

        [Fact]
        public void WriteIfd_WritesTypeForEachEntry()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>()
                {
                    new TiffIfdEntry(TiffTags.ImageWidth, TiffType.Long, 1, new byte[] { 1, 2, 3, 4 }),
                    new TiffIfdEntry(TiffTags.ImageLength, TiffType.Short, 2, new byte[] { 5, 6, 7, 8 }),
                    new TiffIfdEntry(TiffTags.Compression, TiffType.Ascii, 4, new byte[] { (byte)'A', (byte)'B', (byte)'C', 0 })
                };

            using (TiffWriter writer = new TiffWriter(stream))
            {
                long nextIfdMarker = encoder.WriteIfd(writer, entries);
            }

            var ifdEntry1Bytes = stream.ToArray().Skip(4 + 12 * 0).Take(2).ToArray();
            var ifdEntry2Bytes = stream.ToArray().Skip(4 + 12 * 1).Take(2).ToArray();
            var ifdEntry3Bytes = stream.ToArray().Skip(4 + 12 * 2).Take(2).ToArray();

            Assert.Equal(new byte[] { 4, 0 }, ifdEntry1Bytes);
            Assert.Equal(new byte[] { 3, 0 }, ifdEntry2Bytes);
            Assert.Equal(new byte[] { 2, 0 }, ifdEntry3Bytes);
        }

        [Fact]
        public void WriteIfd_WritesCountForEachEntry()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>()
                {
                    new TiffIfdEntry(TiffTags.ImageWidth, TiffType.Long, 1, new byte[] { 1, 2, 3, 4 }),
                    new TiffIfdEntry(TiffTags.ImageLength, TiffType.Short, 2, new byte[] { 5, 6, 7, 8 }),
                    new TiffIfdEntry(TiffTags.Compression, TiffType.Ascii, 4, new byte[] { (byte)'A', (byte)'B', (byte)'C', 0 })
                };

            using (TiffWriter writer = new TiffWriter(stream))
            {
                long nextIfdMarker = encoder.WriteIfd(writer, entries);
            }

            var ifdEntry1Bytes = stream.ToArray().Skip(6 + 12 * 0).Take(4).ToArray();
            var ifdEntry2Bytes = stream.ToArray().Skip(6 + 12 * 1).Take(4).ToArray();
            var ifdEntry3Bytes = stream.ToArray().Skip(6 + 12 * 2).Take(4).ToArray();

            Assert.Equal(new byte[] { 1, 0, 0, 0 }, ifdEntry1Bytes);
            Assert.Equal(new byte[] { 2, 0, 0, 0 }, ifdEntry2Bytes);
            Assert.Equal(new byte[] { 4, 0, 0, 0 }, ifdEntry3Bytes);
        }

        [Fact]
        public void WriteIfd_WritesDataInline()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>()
                {
                    new TiffIfdEntry(TiffTags.ImageWidth, TiffType.Long, 1, new byte[] { 1, 2, 3, 4 }),
                    new TiffIfdEntry(TiffTags.ImageLength, TiffType.Short, 2, new byte[] { 5, 6, 7, 8 }),
                    new TiffIfdEntry(TiffTags.Compression, TiffType.Ascii, 3, new byte[] { (byte)'A', (byte)'B', 0 })
                };

            using (TiffWriter writer = new TiffWriter(stream))
            {
                long nextIfdMarker = encoder.WriteIfd(writer, entries);
            }

            var ifdEntry1Bytes = stream.ToArray().Skip(10 + 12 * 0).Take(4).ToArray();
            var ifdEntry2Bytes = stream.ToArray().Skip(10 + 12 * 1).Take(4).ToArray();
            var ifdEntry3Bytes = stream.ToArray().Skip(10 + 12 * 2).Take(4).ToArray();

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, ifdEntry1Bytes);
            Assert.Equal(new byte[] { 5, 6, 7, 8 }, ifdEntry2Bytes);
            Assert.Equal(new byte[] { (byte)'A', (byte)'B', 0, 0 }, ifdEntry3Bytes);
        }

        [Fact]
        public void WriteIfd_WritesDataByReference()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>()
                {
                    new TiffIfdEntry(TiffTags.ImageWidth, TiffType.Byte, 8, new byte[] { 1, 2, 3, 4, 4, 3, 2, 1 }),
                    new TiffIfdEntry(TiffTags.ImageLength, TiffType.Short, 4, new byte[] { 5, 6, 7, 8, 9, 10, 11, 12 }),
                    new TiffIfdEntry(TiffTags.Compression, TiffType.Ascii, 3, new byte[] { (byte)'A', (byte)'B', 0 })
                };

            using (TiffWriter writer = new TiffWriter(stream))
            {
                writer.Write(new byte[] { 1, 2, 3, 4 });
                long nextIfdMarker = encoder.WriteIfd(writer, entries);
            }

            var ifdEntry1Bytes = stream.ToArray().Skip(14 + 12 * 0).Take(4).ToArray();
            var ifdEntry1Data = stream.ToArray().Skip(46).Take(8).ToArray();
            var ifdEntry2Bytes = stream.ToArray().Skip(14 + 12 * 1).Take(4).ToArray();
            var ifdEntry2Data = stream.ToArray().Skip(54).Take(8).ToArray();
            var ifdEntry3Bytes = stream.ToArray().Skip(14 + 12 * 2).Take(4).ToArray();

            Assert.Equal(new byte[] { 46, 0, 0, 0 }, ifdEntry1Bytes);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 4, 3, 2, 1 }, ifdEntry1Data);
            Assert.Equal(new byte[] { 54, 0, 0, 0 }, ifdEntry2Bytes);
            Assert.Equal(new byte[] { 5, 6, 7, 8, 9, 10, 11, 12 }, ifdEntry2Data);
            Assert.Equal(new byte[] { (byte)'A', (byte)'B', 0, 0 }, ifdEntry3Bytes);
        }

        [Fact]
        public void WriteIfd_WritesDataByReferenceOnWordBoundary()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>()
                {
                    new TiffIfdEntry(TiffTags.ImageWidth, TiffType.Byte, 8, new byte[] { 1, 2, 3, 4, 5 }),
                    new TiffIfdEntry(TiffTags.ImageLength, TiffType.Short, 4, new byte[] { 5, 6, 7, 8, 9, 10, 11, 12 }),
                    new TiffIfdEntry(TiffTags.Compression, TiffType.Ascii, 3, new byte[] { (byte)'A', (byte)'B', 0 })
                };

            using (TiffWriter writer = new TiffWriter(stream))
            {
                writer.Write(new byte[] { 1, 2, 3, 4 });
                long nextIfdMarker = encoder.WriteIfd(writer, entries);
            }

            var ifdEntry1Bytes = stream.ToArray().Skip(14 + 12 * 0).Take(4).ToArray();
            var ifdEntry1Data = stream.ToArray().Skip(46).Take(5).ToArray();
            var ifdEntry2Bytes = stream.ToArray().Skip(14 + 12 * 1).Take(4).ToArray();
            var ifdEntry2Data = stream.ToArray().Skip(52).Take(8).ToArray();
            var ifdEntry3Bytes = stream.ToArray().Skip(14 + 12 * 2).Take(4).ToArray();

            Assert.Equal(new byte[] { 46, 0, 0, 0 }, ifdEntry1Bytes);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, ifdEntry1Data);
            Assert.Equal(new byte[] { 52, 0, 0, 0 }, ifdEntry2Bytes);
            Assert.Equal(new byte[] { 5, 6, 7, 8, 9, 10, 11, 12 }, ifdEntry2Data);
            Assert.Equal(new byte[] { (byte)'A', (byte)'B', 0, 0 }, ifdEntry3Bytes);
        }

        [Fact]
        public void WriteIfd_WritesEntriesInCorrectOrder()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>()
                {
                    new TiffIfdEntry(10, TiffType.Long, 1, new byte[] { 1, 2, 3, 4 }),
                    new TiffIfdEntry(30, TiffType.Long, 1, new byte[] { 5, 6, 7, 8 }),
                    new TiffIfdEntry(20, TiffType.Long, 1, new byte[] { 9, 10, 11, 12 })
                };

            using (TiffWriter writer = new TiffWriter(stream))
            {
                long nextIfdMarker = encoder.WriteIfd(writer, entries);
            }

            var ifdEntry1Bytes = stream.ToArray().Skip(2 + 12 * 0).Take(2).ToArray();
            var ifdEntry2Bytes = stream.ToArray().Skip(2 + 12 * 1).Take(2).ToArray();
            var ifdEntry3Bytes = stream.ToArray().Skip(2 + 12 * 2).Take(2).ToArray();

            Assert.Equal(new byte[] { 10, 0 }, ifdEntry1Bytes);
            Assert.Equal(new byte[] { 20, 0 }, ifdEntry2Bytes);
            Assert.Equal(new byte[] { 30, 0 }, ifdEntry3Bytes);
        }

        [Fact]
        public void WriteIfd_ThrowsException_IfNoEntriesArePresent()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            List<TiffIfdEntry> entries = new List<TiffIfdEntry>();

            using (TiffWriter writer = new TiffWriter(stream))
            {
                ArgumentException e = Assert.Throws<ArgumentException>(() => { encoder.WriteIfd(writer, entries); });

                Assert.Equal($"There must be at least one entry per IFD.{Environment.NewLine}Parameter name: entries", e.Message);
                Assert.Equal("entries", e.ParamName);
            }
        }
    }
}