// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Data structure for holding details of each TIFF IFD entry.
    /// TODO: join with <see cref="Metadata.Profiles.Exif.ExifValue"/>
    /// </summary>
    public class TiffIfdEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffIfdEntry" /> class.
        /// </summary>
        /// <param name="tag">The Tag ID for this entry.</param>
        /// <param name="type">The data-type of this entry.</param>
        /// <param name="count">The number of array items in this entry.</param>
        /// <param name="rawValue">The raw value or offset.</param>
        internal TiffIfdEntry(TiffTagId tag, TiffTagType type, uint count, byte[] rawValue)
            : this((ushort)tag, type, count, rawValue)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffIfdEntry" /> class.
        /// </summary>
        /// <param name="tag">The Tag ID for this entry.</param>
        /// <param name="type">The data-type of this entry.</param>
        /// <param name="count">The number of array items in this entry.</param>
        /// <param name="rawValue">The raw value or offset.</param>
        internal TiffIfdEntry(ushort tag, TiffTagType type, uint count, byte[] rawValue)
        {
            this.Tag = tag;
            this.Type = type;
            this.Count = count;
            this.RawValue = rawValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffIfdEntry" /> class.
        /// </summary>
        /// <param name="tag">The Tag ID for this entry.</param>
        /// <param name="type">The data-type of this entry.</param>
        /// <param name="count">The number of array items in this entry.</param>
        private TiffIfdEntry(ushort tag, TiffTagType type, uint count)
        {
            this.Tag = tag;
            this.Type = type;
            this.Count = count;
            this.RawValue = null;
        }

        /// <summary>
        /// Gets the Tag ID for this entry. See <see cref="TiffTagId" /> for typical values.
        /// </summary>
        public ushort Tag { get; }

        /// <summary>
        /// Gets the tag identifier.
        /// </summary>
        public TiffTagId TagId => (TiffTagId)this.Tag;

        /// <summary>
        /// Gets the data-type of this entry.
        /// </summary>
        public TiffTagType Type { get; }

        /// <summary>
        /// Gets the number of array items in this entry, or one if only a single value.
        /// </summary>
        public uint Count { get; }

        /// <summary>
        /// Gets the size of data.
        /// </summary>
        public uint SizeOfData => GetSizeOfData(this);

        /// <summary>
        /// Gets a value indicating whether this instance has external data.
        /// </summary>
        public bool HasExtData => this.SizeOfData > 4;

        /// <summary>
        /// Gets the raw byte data for this entry (directly value or offset to the extended raw data).
        /// TODO: only for compatibility with encoder
        /// </summary>
        public byte[] RawValue { get; }

        /// <summary>
        /// Gets the value data of the tag.
        /// </summary>
        public object Value { get; private set; }

        /// <summary>
        /// Reads the specified stream.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        internal static TiffIfdEntry Read(TiffStream stream)
        {
            ushort tag = stream.ReadUInt16();
            var type = (TiffTagType)stream.ReadUInt16();
            uint count = stream.ReadUInt32();

            var entry = new TiffIfdEntry(tag, type, count);
            entry.ReadEmbeddedValue(stream);

            return entry;
        }

        internal void ReadExtValueData(TiffStream stream)
        {
            if (!this.HasExtData)
            {
                return;
            }

            (TiffTagId, uint) offset = stream.Context.ExtOffsets.First(t => t.Item1 == this.TagId);
            DebugGuard.IsTrue(this.Value == null, nameof(this.Value));
            DebugGuard.MustBeGreaterThanOrEqualTo(offset.Item2, (uint)TiffConstants.SizeOfTiffHeader, nameof(offset));

            stream.Context.ExtOffsets.Remove(offset);
            stream.Seek(offset.Item2);
            this.Value = this.ReadValue(stream);
        }

        private void ReadEmbeddedValue(TiffStream stream)
        {
            if (this.HasExtData)
            {
                uint offset = stream.ReadUInt32();
                stream.Context.ExtOffsets.Add((this.TagId, offset));
                return;
            }

            long pos = stream.Position;
            object value = this.ReadValue(stream);
            if (value == null)
            {
                // read unknown type value
                value = stream.ReadBytes(4);
            }
            else
            {
                int leftBytes = 4 - (int)(stream.Position - pos);
                if (leftBytes > 0)
                {
                    stream.Skip(leftBytes);
                }
                else if (leftBytes < 0)
                {
                    throw new InvalidDataException("Out of range of IFD entry structure.");
                }
            }

            this.Value = value;
        }

        /// <summary>
        /// Calculates the size (in bytes) of the data contained within an IFD entry.
        /// </summary>
        /// <param name="entry">The IFD entry to calculate the size for.</param>
        /// <returns>The size of the data (in bytes).</returns>
        private static uint GetSizeOfData(TiffIfdEntry entry) => SizeOfDataType(entry.Type) * entry.Count;

        /// <summary>
        /// Calculates the size (in bytes) for the specified TIFF data-type.
        /// </summary>
        /// <param name="type">The data-type to calculate the size for.</param>
        /// <returns>The size of the data-type (in bytes).</returns>
        private static uint SizeOfDataType(TiffTagType type)
        {
            switch (type)
            {
                case TiffTagType.Byte:
                case TiffTagType.Ascii:
                case TiffTagType.SByte:
                case TiffTagType.Undefined:
                    return 1u;
                case TiffTagType.Short:
                case TiffTagType.SShort:
                    return 2u;
                case TiffTagType.Long:
                case TiffTagType.SLong:
                case TiffTagType.Float:
                case TiffTagType.Ifd:
                    return 4u;
                case TiffTagType.Rational:
                case TiffTagType.SRational:
                case TiffTagType.Double:
                    return 8u;
                default:
                    return 0u;
            }
        }
    }
}
