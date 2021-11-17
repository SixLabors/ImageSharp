// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal class EntryReader : BaseExifReader
    {
        private readonly uint startOffset;

        private readonly SortedList<uint, Action> lazyLoaders;

        public EntryReader(Stream stream, ByteOrder byteOrder, uint ifdOffset, SortedList<uint, Action> lazyLoaders)
            : base(stream)
        {
            this.IsBigEndian = byteOrder == ByteOrder.BigEndian;
            this.startOffset = ifdOffset;
            this.lazyLoaders = lazyLoaders;
        }

        public List<IExifValue> Values { get; } = new List<IExifValue>();

        public uint NextIfdOffset { get; private set; }

        public void ReadTags()
        {
            this.ReadValues(this.Values, this.startOffset);
            this.NextIfdOffset = this.ReadUInt32();

            this.ReadSubIfd(this.Values);
        }

        protected override void RegisterExtLoader(uint offset, Action reader) =>
            this.lazyLoaders.Add(offset, reader);
    }

    internal class HeaderReader : BaseExifReader
    {
        public HeaderReader(Stream stream, ByteOrder byteOrder)
            : base(stream) =>
            this.IsBigEndian = byteOrder == ByteOrder.BigEndian;

        public uint FirstIfdOffset { get; private set; }

        public uint ReadFileHeader()
        {
            ushort magic = this.ReadUInt16();
            if (magic != TiffConstants.HeaderMagicNumber)
            {
                TiffThrowHelper.ThrowInvalidHeader();
            }

            this.FirstIfdOffset = this.ReadUInt32();
            return this.FirstIfdOffset;
        }

        protected override void RegisterExtLoader(uint offset, Action reader) => throw new NotSupportedException();
    }
}
