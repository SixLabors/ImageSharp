// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff;

internal class EntryReader : BaseExifReader
{
    public EntryReader(Stream stream, ByteOrder byteOrder, MemoryAllocator allocator)
        : base(stream, allocator) =>
        this.IsBigEndian = byteOrder == ByteOrder.BigEndian;

    public List<IExifValue> Values { get; } = [];

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
        }
    }

    public void ReadBigValues() => this.ReadBigValues(this.Values);
}

internal class HeaderReader : BaseExifReader
{
    public HeaderReader(Stream stream, ByteOrder byteOrder)
        : base(stream, null) =>
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

            ushort byteSize = this.ReadUInt16();
            ushort reserve = this.ReadUInt16();
            if (byteSize == TiffConstants.BigTiffByteSize && reserve == 0)
            {
                this.FirstIfdOffset = this.ReadUInt64();
                return;
            }
        }

        TiffThrowHelper.ThrowInvalidHeader();
    }
}
