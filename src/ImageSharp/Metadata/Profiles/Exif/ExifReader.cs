// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

internal class ExifReader : BaseExifReader
{
    public ExifReader(byte[] exifData)
        : this(exifData, null)
    {
    }

    public ExifReader(byte[] exifData, MemoryAllocator? allocator)
        : base(new MemoryStream(exifData ?? throw new ArgumentNullException(nameof(exifData))), allocator)
    {
        // TODO: We never call this constructor passing a non-null allocator.
    }

    /// <summary>
    /// Reads and returns the collection of EXIF values.
    /// </summary>
    /// <returns>
    /// The <see cref="Collection{ExifValue}"/>.
    /// </returns>
    public List<IExifValue> ReadValues()
    {
        List<IExifValue> values = new();

        // II == 0x4949
        this.IsBigEndian = this.ReadUInt16() != 0x4949;

        if (this.ReadUInt16() != 0x002A)
        {
            return values;
        }

        uint ifdOffset = this.ReadUInt32();
        this.ReadValues(values, ifdOffset);

        uint thumbnailOffset = this.ReadUInt32();
        this.GetThumbnail(thumbnailOffset);

        this.ReadSubIfd(values);

        this.ReadBigValues(values);

        return values;
    }

    private void GetThumbnail(uint offset)
    {
        if (offset == 0)
        {
            return;
        }

        List<IExifValue> values = new();
        this.ReadValues(values, offset);

        for (int i = 0; i < values.Count; i++)
        {
            ExifValue value = (ExifValue)values[i];
            if (value == ExifTag.JPEGInterchangeFormat)
            {
                this.ThumbnailOffset = ((ExifLong)value).Value;
            }
            else if (value == ExifTag.JPEGInterchangeFormatLength)
            {
                this.ThumbnailLength = ((ExifLong)value).Value;
            }
        }
    }
}

/// <summary>
/// Reads and parses EXIF data from a stream.
/// </summary>
internal abstract class BaseExifReader
{
    private readonly MemoryAllocator? allocator;
    private readonly Stream data;
    private List<ExifTag>? invalidTags;

    private List<ulong>? subIfds;

    protected BaseExifReader(Stream stream, MemoryAllocator? allocator)
    {
        this.data = stream ?? throw new ArgumentNullException(nameof(stream));
        this.allocator = allocator;
    }

    private delegate TDataType ConverterMethod<TDataType>(ReadOnlySpan<byte> data);

    /// <summary>
    /// Gets the invalid tags.
    /// </summary>
    public IReadOnlyList<ExifTag> InvalidTags => this.invalidTags ?? (IReadOnlyList<ExifTag>)Array.Empty<ExifTag>();

    /// <summary>
    /// Gets or sets the thumbnail length in the byte stream.
    /// </summary>
    public uint ThumbnailLength { get; protected set; }

    /// <summary>
    /// Gets or sets the thumbnail offset position in the byte stream.
    /// </summary>
    public uint ThumbnailOffset { get; protected set; }

    public bool IsBigEndian { get; protected set; }

    public List<(ulong Offset, ExifDataType DataType, ulong NumberOfComponents, ExifValue Exif)> BigValues { get; } = new();

    protected void ReadBigValues(List<IExifValue> values)
    {
        if (this.BigValues.Count == 0)
        {
            return;
        }

        int maxSize = 0;
        foreach ((ulong offset, ExifDataType dataType, ulong numberOfComponents, ExifValue exif) in this.BigValues)
        {
            ulong size = numberOfComponents * ExifDataTypes.GetSize(dataType);
            DebugGuard.MustBeLessThanOrEqualTo<ulong>(size, int.MaxValue, nameof(size));

            if ((int)size > maxSize)
            {
                maxSize = (int)size;
            }
        }

        if (this.allocator != null)
        {
            // tiff, bigTiff
            using IMemoryOwner<byte> memory = this.allocator.Allocate<byte>(maxSize);
            Span<byte> buf = memory.GetSpan();
            foreach ((ulong Offset, ExifDataType DataType, ulong NumberOfComponents, ExifValue Exif) tag in this.BigValues)
            {
                ulong size = tag.NumberOfComponents * ExifDataTypes.GetSize(tag.DataType);
                this.ReadBigValue(values, tag, buf[..(int)size]);
            }
        }
        else
        {
            // embedded exif
            Span<byte> buf = maxSize <= 256 ? stackalloc byte[256] : new byte[maxSize];
            foreach ((ulong Offset, ExifDataType DataType, ulong NumberOfComponents, ExifValue Exif) tag in this.BigValues)
            {
                ulong size = tag.NumberOfComponents * ExifDataTypes.GetSize(tag.DataType);
                this.ReadBigValue(values, tag, buf[..(int)size]);
            }
        }

        this.BigValues.Clear();
    }

    /// <summary>
    /// Reads the values to the values collection.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="offset">The IFD offset.</param>
    protected void ReadValues(List<IExifValue> values, uint offset)
    {
        if (offset > this.data.Length)
        {
            return;
        }

        this.Seek(offset);
        int count = this.ReadUInt16();

        Span<byte> offsetBuffer = stackalloc byte[4];
        for (int i = 0; i < count; i++)
        {
            this.ReadValue(values, offsetBuffer);
        }
    }

    protected void ReadSubIfd(List<IExifValue> values)
    {
        if (this.subIfds != null)
        {
            const int maxSubIfds = 8;
            const int maxNestingLevel = 8;
            Span<ulong> buf = stackalloc ulong[maxSubIfds];
            for (int i = 0; i < maxNestingLevel && this.subIfds.Count > 0; i++)
            {
                int sz = Math.Min(this.subIfds.Count, maxSubIfds);
                CollectionsMarshal.AsSpan(this.subIfds)[..sz].CopyTo(buf);

                this.subIfds.Clear();
                foreach (ulong subIfdOffset in buf[..sz])
                {
                    this.ReadValues(values, (uint)subIfdOffset);
                }
            }
        }
    }

    protected void ReadValues64(List<IExifValue> values, ulong offset)
    {
        DebugGuard.MustBeLessThanOrEqualTo(offset, (ulong)this.data.Length, "By spec UInt64.MaxValue is supported, but .NET Stream.Length can Int64.MaxValue.");

        this.Seek(offset);
        ulong count = this.ReadUInt64();

        Span<byte> offsetBuffer = stackalloc byte[8];
        for (ulong i = 0; i < count; i++)
        {
            this.ReadValue64(values, offsetBuffer);
        }
    }

    protected void ReadBigValue(IList<IExifValue> values, (ulong Offset, ExifDataType DataType, ulong NumberOfComponents, ExifValue Exif) tag, Span<byte> buffer)
    {
        this.Seek(tag.Offset);
        if (this.TryReadSpan(buffer))
        {
            object? value = this.ConvertValue(tag.DataType, buffer, tag.NumberOfComponents > 1 || tag.Exif.IsArray);
            this.Add(values, tag.Exif, value);
        }
    }

    private static TDataType[] ToArray<TDataType>(ExifDataType dataType, ReadOnlySpan<byte> data, ConverterMethod<TDataType> converter)
    {
        int dataTypeSize = (int)ExifDataTypes.GetSize(dataType);
        int length = data.Length / dataTypeSize;

        TDataType[] result = new TDataType[length];

        for (int i = 0; i < length; i++)
        {
            ReadOnlySpan<byte> buffer = data.Slice(i * dataTypeSize, dataTypeSize);

            result.SetValue(converter(buffer), i);
        }

        return result;
    }

    private static string ConvertToString(Encoding encoding, ReadOnlySpan<byte> buffer)
    {
        int nullCharIndex = buffer.IndexOf((byte)0);

        if (nullCharIndex > -1)
        {
            buffer = buffer[..nullCharIndex];
        }

        return encoding.GetString(buffer);
    }

    private static byte ConvertToByte(ReadOnlySpan<byte> buffer) => buffer[0];

    private object? ConvertValue(ExifDataType dataType, ReadOnlySpan<byte> buffer, bool isArray)
    {
        if (buffer.Length == 0)
        {
            return null;
        }

        switch (dataType)
        {
            case ExifDataType.Unknown:
                return null;
            case ExifDataType.Ascii:
                return ConvertToString(ExifConstants.DefaultEncoding, buffer);
            case ExifDataType.Byte:
            case ExifDataType.Undefined:
                if (!isArray)
                {
                    return ConvertToByte(buffer);
                }

                return buffer.ToArray();
            case ExifDataType.DoubleFloat:
                if (!isArray)
                {
                    return this.ConvertToDouble(buffer);
                }

                return ToArray(dataType, buffer, this.ConvertToDouble);
            case ExifDataType.Long:
            case ExifDataType.Ifd:
                if (!isArray)
                {
                    return this.ConvertToUInt32(buffer);
                }

                return ToArray(dataType, buffer, this.ConvertToUInt32);
            case ExifDataType.Rational:
                if (!isArray)
                {
                    return this.ToRational(buffer);
                }

                return ToArray(dataType, buffer, this.ToRational);
            case ExifDataType.Short:
                if (!isArray)
                {
                    return this.ConvertToShort(buffer);
                }

                return ToArray(dataType, buffer, this.ConvertToShort);
            case ExifDataType.SignedByte:
                if (!isArray)
                {
                    return this.ConvertToSignedByte(buffer);
                }

                return ToArray(dataType, buffer, this.ConvertToSignedByte);
            case ExifDataType.SignedLong:
                if (!isArray)
                {
                    return this.ConvertToInt32(buffer);
                }

                return ToArray(dataType, buffer, this.ConvertToInt32);
            case ExifDataType.SignedRational:
                if (!isArray)
                {
                    return this.ToSignedRational(buffer);
                }

                return ToArray(dataType, buffer, this.ToSignedRational);
            case ExifDataType.SignedShort:
                if (!isArray)
                {
                    return this.ConvertToSignedShort(buffer);
                }

                return ToArray(dataType, buffer, this.ConvertToSignedShort);
            case ExifDataType.SingleFloat:
                if (!isArray)
                {
                    return this.ConvertToSingle(buffer);
                }

                return ToArray(dataType, buffer, this.ConvertToSingle);
            case ExifDataType.Long8:
            case ExifDataType.Ifd8:
                if (!isArray)
                {
                    return this.ConvertToUInt64(buffer);
                }

                return ToArray(dataType, buffer, this.ConvertToUInt64);
            case ExifDataType.SignedLong8:
                if (!isArray)
                {
                    return this.ConvertToInt64(buffer);
                }

                return ToArray(dataType, buffer, this.ConvertToUInt64);

            default:
                throw new NotSupportedException($"Data type {dataType} is not supported.");
        }
    }

    private void ReadValue(List<IExifValue> values, Span<byte> offsetBuffer)
    {
        // 2   | 2    | 4     | 4
        // tag | type | count | value offset
        if ((this.data.Length - this.data.Position) < 12)
        {
            return;
        }

        ExifTagValue tag = (ExifTagValue)this.ReadUInt16();
        ExifDataType dataType = EnumUtils.Parse(this.ReadUInt16(), ExifDataType.Unknown);

        uint numberOfComponents = this.ReadUInt32();

        this.TryReadSpan(offsetBuffer);

        // Ensure that the data type is valid
        if (dataType == ExifDataType.Unknown)
        {
            return;
        }

        // Issue #132: ExifDataType == Undefined is treated like a byte array.
        // If numberOfComponents == 0 this value can only be handled as an inline value and must fallback to 4 (bytes)
        if (numberOfComponents == 0)
        {
            numberOfComponents = 4 / ExifDataTypes.GetSize(dataType);
        }

        ExifValue? exifValue = ExifValues.Create(tag) ?? ExifValues.Create(tag, dataType, numberOfComponents);

        if (exifValue is null)
        {
            this.AddInvalidTag(new UnkownExifTag(tag));
            return;
        }

        uint size = numberOfComponents * ExifDataTypes.GetSize(dataType);
        if (size > 4)
        {
            uint newOffset = this.ConvertToUInt32(offsetBuffer);

            // Ensure that the new index does not overrun the data.
            if (newOffset > int.MaxValue || (newOffset + size) > this.data.Length)
            {
                this.AddInvalidTag(new UnkownExifTag(tag));
                return;
            }

            this.BigValues.Add((newOffset, dataType, numberOfComponents, exifValue));
        }
        else
        {
            object? value = this.ConvertValue(dataType, offsetBuffer[..(int)size], numberOfComponents > 1 || exifValue.IsArray);
            this.Add(values, exifValue, value);
        }
    }

    private void ReadValue64(List<IExifValue> values, Span<byte> offsetBuffer)
    {
        if ((this.data.Length - this.data.Position) < 20)
        {
            return;
        }

        ExifTagValue tag = (ExifTagValue)this.ReadUInt16();
        ExifDataType dataType = EnumUtils.Parse(this.ReadUInt16(), ExifDataType.Unknown);

        ulong numberOfComponents = this.ReadUInt64();

        this.TryReadSpan(offsetBuffer);

        if (dataType == ExifDataType.Unknown)
        {
            return;
        }

        if (numberOfComponents == 0)
        {
            numberOfComponents = 8 / ExifDataTypes.GetSize(dataType);
        }

        ExifValue? exifValue = tag switch
        {
            ExifTagValue.StripOffsets => new ExifLong8Array(ExifTagValue.StripOffsets),
            ExifTagValue.StripByteCounts => new ExifLong8Array(ExifTagValue.StripByteCounts),
            ExifTagValue.TileOffsets => new ExifLong8Array(ExifTagValue.TileOffsets),
            ExifTagValue.TileByteCounts => new ExifLong8Array(ExifTagValue.TileByteCounts),
            _ => ExifValues.Create(tag) ?? ExifValues.Create(tag, dataType, numberOfComponents),
        };

        if (exifValue is null)
        {
            this.AddInvalidTag(new UnkownExifTag(tag));
            return;
        }

        ulong size = numberOfComponents * ExifDataTypes.GetSize(dataType);
        if (size > 8)
        {
            ulong newOffset = this.ConvertToUInt64(offsetBuffer);
            if (newOffset > ulong.MaxValue || newOffset > ((ulong)this.data.Length - size))
            {
                this.AddInvalidTag(new UnkownExifTag(tag));
                return;
            }

            this.BigValues.Add((newOffset, dataType, numberOfComponents, exifValue));
        }
        else
        {
            object? value = this.ConvertValue(dataType, offsetBuffer[..(int)size], numberOfComponents > 1 || exifValue.IsArray);
            this.Add(values, exifValue, value);
        }
    }

    private void Add(IList<IExifValue> values, ExifValue exif, object? value)
    {
        if (!exif.TrySetValue(value))
        {
            return;
        }

        foreach (IExifValue val in values)
        {
            // to skip duplicates must be used Equals method,
            // == operator not defined for ExifValue and IExifValue
            if (exif.Equals(val))
            {
                Debug.WriteLine($"Duplicate Exif tag: tag={exif.Tag}, dataType={exif.DataType}");
                return;
            }
        }

        if (exif.Tag == ExifTag.SubIFDOffset)
        {
            this.AddSubIfd(value);
        }
        else if (exif.Tag == ExifTag.GPSIFDOffset)
        {
            this.AddSubIfd(value);
        }
        else
        {
            values.Add(exif);
        }
    }

    private void AddInvalidTag(ExifTag tag)
        => (this.invalidTags ??= new List<ExifTag>()).Add(tag);

    private void AddSubIfd(object? val)
        => (this.subIfds ??= new List<ulong>()).Add(Convert.ToUInt64(val, CultureInfo.InvariantCulture));

    private void Seek(ulong pos)
        => this.data.Seek((long)pos, SeekOrigin.Begin);

    private bool TryReadSpan(Span<byte> span)
    {
        int length = span.Length;
        if ((this.data.Length - this.data.Position) < length)
        {
            return false;
        }

        int read = this.data.Read(span);
        return read == length;
    }

    protected ulong ReadUInt64()
    {
        Span<byte> buffer = stackalloc byte[8];

        return this.TryReadSpan(buffer)
            ? this.ConvertToUInt64(buffer)
            : default;
    }

    // Known as Long in Exif Specification.
    protected uint ReadUInt32()
    {
        Span<byte> buffer = stackalloc byte[4];

        return this.TryReadSpan(buffer)
            ? this.ConvertToUInt32(buffer)
            : default;
    }

    protected ushort ReadUInt16()
    {
        Span<byte> buffer = stackalloc byte[2];

        return this.TryReadSpan(buffer)
            ? this.ConvertToShort(buffer)
            : default;
    }

    private long ConvertToInt64(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 8)
        {
            return default;
        }

        return this.IsBigEndian
            ? BinaryPrimitives.ReadInt64BigEndian(buffer)
            : BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    private ulong ConvertToUInt64(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 8)
        {
            return default;
        }

        return this.IsBigEndian
            ? BinaryPrimitives.ReadUInt64BigEndian(buffer)
            : BinaryPrimitives.ReadUInt64LittleEndian(buffer);
    }

    private double ConvertToDouble(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 8)
        {
            return default;
        }

        long intValue = this.IsBigEndian
            ? BinaryPrimitives.ReadInt64BigEndian(buffer)
            : BinaryPrimitives.ReadInt64LittleEndian(buffer);

        return Unsafe.As<long, double>(ref intValue);
    }

    private uint ConvertToUInt32(ReadOnlySpan<byte> buffer)
    {
        // Known as Long in Exif Specification.
        if (buffer.Length < 4)
        {
            return default;
        }

        return this.IsBigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(buffer)
            : BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    private ushort ConvertToShort(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 2)
        {
            return default;
        }

        return this.IsBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(buffer)
            : BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    private float ConvertToSingle(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 4)
        {
            return default;
        }

        int intValue = this.IsBigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(buffer)
            : BinaryPrimitives.ReadInt32LittleEndian(buffer);

        return Unsafe.As<int, float>(ref intValue);
    }

    private Rational ToRational(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 8)
        {
            return default;
        }

        uint numerator = this.ConvertToUInt32(buffer[..4]);
        uint denominator = this.ConvertToUInt32(buffer.Slice(4, 4));

        return new Rational(numerator, denominator, false);
    }

    private sbyte ConvertToSignedByte(ReadOnlySpan<byte> buffer) => unchecked((sbyte)buffer[0]);

    private int ConvertToInt32(ReadOnlySpan<byte> buffer) // SignedLong in Exif Specification
    {
        if (buffer.Length < 4)
        {
            return default;
        }

        return this.IsBigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(buffer)
            : BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    private SignedRational ToSignedRational(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 8)
        {
            return default;
        }

        int numerator = this.ConvertToInt32(buffer[..4]);
        int denominator = this.ConvertToInt32(buffer.Slice(4, 4));

        return new SignedRational(numerator, denominator, false);
    }

    private short ConvertToSignedShort(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 2)
        {
            return default;
        }

        return this.IsBigEndian
            ? BinaryPrimitives.ReadInt16BigEndian(buffer)
            : BinaryPrimitives.ReadInt16LittleEndian(buffer);
    }
}
