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
        private readonly SortedList<ulong, Action> lazyLoaders;

        public EntryReader(Stream stream, ByteOrder byteOrder, SortedList<ulong, Action> lazyLoaders)
            : base(stream)
        {
            this.IsBigEndian = byteOrder == ByteOrder.BigEndian;
            this.lazyLoaders = lazyLoaders;
        }

        public List<IExifValue> Values { get; } = new List<IExifValue>();

        public ulong NextIfdOffset { get; private set; }

        public void ReadTags(bool isBigTiff, ulong ifdOffset)
        {
            if (!isBigTiff)
            {
                this.ReadValues(this.Values, (uint)ifdOffset);
                this.NextIfdOffset = this.ReadUInt32();

                this.ReadSubIfd(this.Values);
            }
            else
            {
                this.ReadValues64(this.Values, ifdOffset);
                this.NextIfdOffset = this.ReadUInt64();

                this.ReadSubIfd64(this.Values);
            }
        }

        protected override void RegisterExtLoader(ulong offset, Action reader) =>
            this.lazyLoaders.Add(offset, reader);
    }

    internal class HeaderReader : BaseExifReader
    {
        public HeaderReader(Stream stream, ByteOrder byteOrder)
            : base(stream) =>
            this.IsBigEndian = byteOrder == ByteOrder.BigEndian;

        public bool IsBigTiff { get; private set; }

        public ulong FirstIfdOffset { get; private set; }

        public void ReadFileHeader()
        {
            ushort magic = this.ReadUInt16();
            if (magic == TiffConstants.HeaderMagicNumber)
            {
                this.IsBigTiff = false;
                this.FirstIfdOffset = this.ReadUInt32();
                return;
            }
            else if (magic == TiffConstants.BigTiffHeaderMagicNumber)
            {
                this.IsBigTiff = true;

                ushort bytesize = this.ReadUInt16();
                ushort reserve = this.ReadUInt16();
                if (bytesize == TiffConstants.BigTiffBytesize && reserve == 0)
                {
                    this.FirstIfdOffset = this.ReadUInt64();
                    return;
                }
            }

            TiffThrowHelper.ThrowInvalidHeader();
        }

        protected override void RegisterExtLoader(ulong offset, Action reader) => throw new NotSupportedException();
    }
}
