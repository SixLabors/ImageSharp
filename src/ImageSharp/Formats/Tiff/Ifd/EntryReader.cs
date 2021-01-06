// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    internal class EntryReader : ExifReader
    {
        private readonly uint startOffset;

        public EntryReader(ByteOrder byteOrder, Stream stream, uint ifdOffset)
            : base(byteOrder == ByteOrder.BigEndian, stream) =>
            this.startOffset = ifdOffset;

        public uint? BigValuesOffset => this.LazyStartOffset;

        public uint NextIfdOffset { get; private set; }

        public override List<IExifValue> ReadValues()
        {
            var values = new List<IExifValue>();
            this.AddValues(values, this.startOffset);

            this.NextIfdOffset = this.ReadUInt32();

            this.AddSubIfdValues(values);
            return values;
        }

        public void LoadBigValues() => this.LazyLoad();
    }

    internal class HeaderReader : ExifReader
    {
        public HeaderReader(ByteOrder byteOrder, Stream stream)
            : base(byteOrder == ByteOrder.BigEndian, stream)
        {
        }

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
    }
}
