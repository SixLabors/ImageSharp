// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The TIFF IFD reader class.
    /// </summary>
    internal class DirectoryReader
    {
        private const int DirectoryMax = 65534;

        private readonly Stream stream;

        private readonly MemoryAllocator allocator;

        private ulong nextIfdOffset;

        public DirectoryReader(Stream stream, MemoryAllocator allocator)
        {
            this.stream = stream;
            this.allocator = allocator;
        }

        /// <summary>
        /// Gets the byte order.
        /// </summary>
        public ByteOrder ByteOrder { get; private set; }

        public bool IsBigTiff { get; private set; }

        /// <summary>
        /// Reads image file directories.
        /// </summary>
        /// <returns>Image file directories.</returns>
        public IEnumerable<ExifProfile> Read()
        {
            this.ByteOrder = ReadByteOrder(this.stream);
            var headerReader = new HeaderReader(this.stream, this.ByteOrder);
            headerReader.ReadFileHeader();

            this.nextIfdOffset = headerReader.FirstIfdOffset;
            this.IsBigTiff = headerReader.IsBigTiff;

            return this.ReadIfds(headerReader.IsBigTiff);
        }

        private static ByteOrder ReadByteOrder(Stream stream)
        {
            Span<byte> headerBytes = stackalloc byte[2];
            stream.Read(headerBytes);
            if (headerBytes[0] == TiffConstants.ByteOrderLittleEndian && headerBytes[1] == TiffConstants.ByteOrderLittleEndian)
            {
                return ByteOrder.LittleEndian;
            }

            if (headerBytes[0] == TiffConstants.ByteOrderBigEndian && headerBytes[1] == TiffConstants.ByteOrderBigEndian)
            {
                return ByteOrder.BigEndian;
            }

            throw TiffThrowHelper.ThrowInvalidHeader();
        }

        private IEnumerable<ExifProfile> ReadIfds(bool isBigTiff)
        {
            var readers = new List<EntryReader>();
            while (this.nextIfdOffset != 0 && this.nextIfdOffset < (ulong)this.stream.Length)
            {
                var reader = new EntryReader(this.stream, this.ByteOrder, this.allocator);
                reader.ReadTags(isBigTiff, this.nextIfdOffset);

                if (reader.BigValues.Count > 0)
                {
                    reader.BigValues.Sort((t1, t2) => t1.Offset.CompareTo(t2.Offset));

                    // this means that most likely all elements are placed  before next IFD
                    if (reader.BigValues[0].Offset < reader.NextIfdOffset)
                    {
                        reader.ReadBigValues();
                    }
                }

                this.nextIfdOffset = reader.NextIfdOffset;
                readers.Add(reader);

                if (readers.Count >= DirectoryMax)
                {
                    TiffThrowHelper.ThrowImageFormatException("TIFF image contains too many directories");
                }
            }

            var list = new List<ExifProfile>(readers.Count);
            foreach (EntryReader reader in readers)
            {
                reader.ReadBigValues();
                var profile = new ExifProfile(reader.Values, reader.InvalidTags);
                list.Add(profile);
            }

            return list;
        }
    }
}
