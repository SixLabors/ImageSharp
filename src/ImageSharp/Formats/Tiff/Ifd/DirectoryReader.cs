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

        private uint nextIfdOffset;

        // used for sequential read big values (actual for multiframe big files)
        // todo: different tags can link to the same data (stream offset) - investigate
        private readonly SortedList<uint, Action> lazyLoaders = new SortedList<uint, Action>(new DuplicateKeyComparer<uint>());

        public DirectoryReader(Stream stream) => this.stream = stream;

        /// <summary>
        /// Gets the byte order.
        /// </summary>
        public ByteOrder ByteOrder { get; private set; }

        /// <summary>
        /// Reads image file directories.
        /// </summary>
        /// <returns>Image file directories.</returns>
        public IEnumerable<ExifProfile> Read()
        {
            this.ByteOrder = ReadByteOrder(this.stream);
            this.nextIfdOffset = new HeaderReader(this.stream, this.ByteOrder).ReadFileHeader();
            return this.ReadIfds();
        }

        private static ByteOrder ReadByteOrder(Stream stream)
        {
            var headerBytes = new byte[2];
            stream.Read(headerBytes, 0, 2);
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

        private IEnumerable<ExifProfile> ReadIfds()
        {
            var readers = new List<EntryReader>();
            while (this.nextIfdOffset != 0 && this.nextIfdOffset < this.stream.Length)
            {
                var reader = new EntryReader(this.stream, this.ByteOrder, this.nextIfdOffset, this.lazyLoaders);
                reader.ReadTags();

                this.nextIfdOffset = reader.NextIfdOffset;

                readers.Add(reader);
            }

            // Sequential reading big values.
            foreach (Action loader in this.lazyLoaders.Values)
            {
                loader();
            }

            var list = new List<ExifProfile>();
            foreach (EntryReader reader in readers)
            {
                var profile = new ExifProfile(reader.Values, reader.InvalidTags);
                list.Add(profile);
            }

            return list;
        }

        /// <summary>
        /// <see cref="DuplicateKeyComparer{TKey}"/> used for possibility add a duplicate offsets (but tags don't duplicate).
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        private class DuplicateKeyComparer<TKey> : IComparer<TKey>
            where TKey : IComparable
        {
            public int Compare(TKey x, TKey y)
            {
                int result = x.CompareTo(y);

                // Handle equality as being greater.
                return (result == 0) ? 1 : result;
            }
        }
    }
}
