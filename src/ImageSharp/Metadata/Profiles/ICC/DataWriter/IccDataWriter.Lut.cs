// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <content>
/// Provides methods to write ICC data types.
/// </content>
internal sealed partial class IccDataWriter
{
    /// <summary>
    /// Writes an 8bit lookup table.
    /// </summary>
    /// <param name="value">The LUT to write.</param>
    /// <returns>The number of bytes written.</returns>
    public int WriteLut8(IccLut value)
    {
        foreach (float item in value.Values)
        {
            this.WriteByte((byte)Numerics.Clamp((item * byte.MaxValue) + 0.5F, 0, byte.MaxValue));
        }

        return value.Values.Length;
    }

    /// <summary>
    /// Writes an 16bit lookup table.
    /// </summary>
    /// <param name="value">The LUT to write.</param>
    /// <returns>The number of bytes written.</returns>
    public int WriteLut16(IccLut value)
    {
        foreach (float item in value.Values)
        {
            this.WriteUInt16((ushort)Numerics.Clamp((item * ushort.MaxValue) + 0.5F, 0, ushort.MaxValue));
        }

        return value.Values.Length * 2;
    }

    /// <summary>
    /// Writes an color lookup table.
    /// </summary>
    /// <param name="value">The CLUT to write.</param>
    /// <returns>The number of bytes written.</returns>
    public int WriteClut(IccClut value)
    {
        int count = this.WriteArray(value.GridPointCount);
        count += this.WriteEmpty(16 - value.GridPointCount.Length);

        switch (value.DataType)
        {
            case IccClutDataType.Float:
                return count + this.WriteClutF32(value);
            case IccClutDataType.UInt8:
                count += this.WriteByte(1);
                count += this.WriteEmpty(3);
                return count + this.WriteClut8(value);
            case IccClutDataType.UInt16:
                count += this.WriteByte(2);
                count += this.WriteEmpty(3);
                return count + this.WriteClut16(value);

            default:
                throw new InvalidIccProfileException($"Invalid CLUT data type of {value.DataType}");
        }
    }

    /// <summary>
    /// Writes a 8bit color lookup table.
    /// </summary>
    /// <param name="value">The CLUT to write.</param>
    /// <returns>The number of bytes written.</returns>
    public int WriteClut8(IccClut value)
    {
        if (value.DataType == IccClutDataType.UInt8 && value.TryGetEncodedValues(out byte[] encoded, out int padding))
        {
            ReadOnlySpan<byte> samples = encoded.AsSpan(padding);
            this.dataStream.Write(samples);
            return samples.Length;
        }

        int count = 0;
        for (int i = 0; i < value.SampleCount; i++)
        {
            count += this.WriteByte((byte)Numerics.Clamp((value.GetSample(i) * byte.MaxValue) + 0.5F, 0, byte.MaxValue));
        }

        return count;
    }

    /// <summary>
    /// Writes a 16bit color lookup table.
    /// </summary>
    /// <param name="value">The CLUT to write.</param>
    /// <returns>The number of bytes written.</returns>
    public int WriteClut16(IccClut value)
    {
        if (value.DataType == IccClutDataType.UInt16 && value.TryGetEncodedValues(out byte[] encoded, out int padding))
        {
            ReadOnlySpan<byte> samples = encoded.AsSpan(padding);
            if (!BitConverter.IsLittleEndian)
            {
                this.dataStream.Write(samples);
                return samples.Length;
            }

            // Stream output needs big-endian words. Reuse a bounded block instead of materializing a float CLUT.
            InlineArray128<ushort> storage = default;
            Span<ushort> buffer = storage;
            ReadOnlySpan<ushort> source = MemoryMarshal.Cast<byte, ushort>(samples);
            while (!source.IsEmpty)
            {
                int length = Math.Min(source.Length, buffer.Length);
                Span<ushort> block = buffer[..length];
                BinaryPrimitives.ReverseEndianness(source[..length], block);
                this.dataStream.Write(MemoryMarshal.AsBytes(block));
                source = source[length..];
            }

            return samples.Length;
        }

        int count = 0;
        for (int i = 0; i < value.SampleCount; i++)
        {
            count += this.WriteUInt16((ushort)Numerics.Clamp((value.GetSample(i) * ushort.MaxValue) + 0.5F, 0, ushort.MaxValue));
        }

        return count;
    }

    /// <summary>
    /// Writes a 32bit float color lookup table.
    /// </summary>
    /// <param name="value">The CLUT to write.</param>
    /// <returns>The number of bytes written.</returns>
    public int WriteClutF32(IccClut value)
    {
        int count = 0;
        for (int i = 0; i < value.SampleCount; i++)
        {
            count += this.WriteSingle(value.GetSample(i));
        }

        return count;
    }
}
