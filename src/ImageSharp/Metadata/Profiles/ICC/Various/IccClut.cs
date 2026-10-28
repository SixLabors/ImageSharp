// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Color Lookup Table.
/// </summary>
internal sealed class IccClut : IEquatable<IccClut>
{
    private readonly byte[] encodedValues;
    private readonly int paddingBytes;
    private readonly int valueCount;
    private readonly float[] values;

    /// <summary>
    /// Initializes a new instance of the <see cref="IccClut"/> class.
    /// </summary>
    /// <param name="values">The CLUT values.</param>
    /// <param name="gridPointCount">The gridpoint count.</param>
    /// <param name="type">The data type of this CLUT.</param>
    /// <param name="outputChannelCount">The output channels count.</param>
    public IccClut(float[] values, byte[] gridPointCount, IccClutDataType type, int outputChannelCount)
    {
        Guard.NotNull(values, nameof(values));
        Guard.NotNull(gridPointCount, nameof(gridPointCount));

        this.values = values;
        this.encodedValues = [];
        this.valueCount = values.Length;
        this.DataType = type;
        this.InputChannelCount = gridPointCount.Length;
        this.OutputChannelCount = outputChannelCount;
        this.GridPointCount = gridPointCount;
        this.CheckValues();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccClut"/> class from complete encoded integer samples.
    /// </summary>
    /// <param name="values">The complete big-endian samples bounded by the ICC reader.</param>
    /// <param name="gridPointCount">The number of nodes on each input axis.</param>
    /// <param name="type">The encoded integer type, UInt8 or UInt16.</param>
    /// <param name="outputChannelCount">The number of samples in each node.</param>
    public IccClut(ReadOnlyMemory<byte> values, byte[] gridPointCount, IccClutDataType type, int outputChannelCount)
    {
        this.values = [];
        this.DataType = type;
        this.InputChannelCount = gridPointCount.Length;
        this.OutputChannelCount = outputChannelCount;
        this.GridPointCount = gridPointCount;
        int sampleBytes = type == IccClutDataType.UInt16 ? sizeof(ushort) : sizeof(byte);
        this.valueCount = values.Length / sampleBytes;
        this.CheckValues();

        // A packed read can include bytes before a node. Own those leading bytes so the first and last
        // nodes can use the same bounded read as every other node. No read reaches outside this array.
        int readBytes = type == IccClutDataType.UInt16 ? sizeof(ulong) : sizeof(uint);
        this.paddingBytes = Math.Max(readBytes - (outputChannelCount * sampleBytes), 0);
        this.encodedValues = new byte[checked(values.Length + this.paddingBytes)];
        Span<byte> destination = this.encodedValues.AsSpan(this.paddingBytes);
        if (type == IccClutDataType.UInt16 && BitConverter.IsLittleEndian)
        {
            // Copy and swap in one pass. The converter reads native integers; serialization restores big-endian bytes.
            BinaryPrimitives.ReverseEndianness(MemoryMarshal.Cast<byte, ushort>(values.Span), MemoryMarshal.Cast<byte, ushort>(destination));
        }
        else
        {
            values.Span.CopyTo(destination);
        }
    }

    /// <summary>
    /// Gets the samples of a float-backed table. Integer-backed tables return an empty span.
    /// </summary>
    public ReadOnlySpan<float> FloatValues => this.values;

    /// <summary>
    /// Gets the number of samples across all grid nodes and output channels.
    /// </summary>
    public int SampleCount => this.valueCount;

    /// <summary>
    /// Gets the CLUT data type (important when writing a profile).
    /// </summary>
    public IccClutDataType DataType { get; }

    /// <summary>
    /// Gets the number of input channels.
    /// </summary>
    public int InputChannelCount { get; }

    /// <summary>
    /// Gets the number of output channels.
    /// </summary>
    public int OutputChannelCount { get; }

    /// <summary>
    /// Gets the number of grid points per input channel.
    /// </summary>
    public byte[] GridPointCount { get; }

    /// <summary>
    /// Reads a normalized sample without allocating or changing the table's representation.
    /// </summary>
    /// <param name="index">The sample index, less than <see cref="SampleCount"/>.</param>
    /// <returns>The float sample or the integer sample divided by its format's maximum code.</returns>
    public float GetSample(int index)
    {
        if (this.encodedValues.Length == 0)
        {
            return this.values[index];
        }

        ReadOnlySpan<byte> samples = this.encodedValues.AsSpan(this.paddingBytes);
        return this.DataType == IccClutDataType.UInt16
            ? MemoryMarshal.Cast<byte, ushort>(samples)[index] / (float)ushort.MaxValue
            : samples[index] / (float)byte.MaxValue;
    }

    /// <summary>
    /// Gets the owned integer storage when this table was read from encoded integer samples.
    /// </summary>
    /// <param name="data">The owned native-endian bytes, including leading read padding.</param>
    /// <param name="padding">The number of leading bytes before the first sample.</param>
    /// <returns>Whether the integer samples can be read directly.</returns>
    public bool TryGetEncodedValues(out byte[] data, out int padding)
    {
        data = this.encodedValues;
        padding = this.paddingBytes;
        return this.encodedValues.Length != 0;
    }

    /// <inheritdoc/>
    public bool Equals(IccClut? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return this.DataType == other.DataType
            && this.InputChannelCount == other.InputChannelCount
            && this.OutputChannelCount == other.OutputChannelCount
            && this.GridPointCount.AsSpan().SequenceEqual(other.GridPointCount)
            && this.EqualsSamples(other);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IccClut other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
            this.valueCount,
            this.DataType,
            this.InputChannelCount,
            this.OutputChannelCount);

    /// <summary>
    /// Compares samples directly in their stored representations without allocating a decoded table.
    /// </summary>
    /// <param name="other">The table to compare.</param>
    /// <returns>Whether both tables have the same normalized samples.</returns>
    private bool EqualsSamples(IccClut other)
    {
        if (this.valueCount != other.valueCount)
        {
            return false;
        }

        if (this.encodedValues.Length != 0 && other.encodedValues.Length != 0)
        {
            return this.encodedValues.AsSpan(this.paddingBytes).SequenceEqual(other.encodedValues.AsSpan(other.paddingBytes));
        }

        if (this.encodedValues.Length == 0 && other.encodedValues.Length == 0)
        {
            return this.values.AsSpan().SequenceEqual(other.values);
        }

        // Integer and float tables can contain the same normalized samples.
        // Compare them directly instead of retaining a second array for equality checks.
        for (int i = 0; i < this.valueCount; i++)
        {
            if (!this.GetSample(i).Equals(other.GetSample(i)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates the complete grid against the stored sample count before allocation or unchecked node reads.
    /// </summary>
    private void CheckValues()
    {
        Guard.MustBeBetweenOrEqualTo(this.InputChannelCount, 1, 15, nameof(this.InputChannelCount));
        Guard.MustBeBetweenOrEqualTo(this.OutputChannelCount, 1, 15, nameof(this.OutputChannelCount));

        // Each grid node stores one value per output channel. Validate the full product before
        // interpolation uses unchecked gathers. Bound each multiplication by the available storage.
        int length = this.OutputChannelCount;
        foreach (byte gridPoints in this.GridPointCount)
        {
            if (gridPoints == 0 || length > this.valueCount / gridPoints)
            {
                throw new InvalidIccProfileException("The CLUT data is shorter than its declared dimensions.");
            }

            length *= gridPoints;
        }

        if (length != this.valueCount)
        {
            throw new InvalidIccProfileException("The CLUT data length does not match its declared dimensions.");
        }
    }
}
