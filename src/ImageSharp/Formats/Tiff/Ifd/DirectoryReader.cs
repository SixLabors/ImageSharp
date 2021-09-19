// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The TIFF IFD reader class.
    /// </summary>
    internal class DirectoryReader
    {
        private readonly Stream stream;

        private ulong nextIfdOffset;

        public DirectoryReader(Stream stream) => this.stream = stream;

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
            else if (headerBytes[0] == TiffConstants.ByteOrderBigEndian && headerBytes[1] == TiffConstants.ByteOrderBigEndian)
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
                var reader = new EntryReader(this.stream, this.ByteOrder);
                reader.ReadTags(isBigTiff, this.nextIfdOffset);

                if (reader.BigValues.Count > 0)
                {
                    reader.BigValues.Sort((t1, t2) => t1.offset.CompareTo(t2.offset));

                    // this means that most likely all elements are placed  before next IFD
                    if (reader.BigValues[0].offset < reader.NextIfdOffset)
                    {
                        reader.ReadBigValues();
                    }
                }

                this.nextIfdOffset = reader.NextIfdOffset;
                readers.Add(reader);
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
