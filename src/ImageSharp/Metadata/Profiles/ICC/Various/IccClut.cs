// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Color Lookup Table.
/// </summary>
internal sealed class IccClut : IEquatable<IccClut>
{
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

        this.Values = values;
        this.DataType = type;
        this.InputChannelCount = gridPointCount.Length;
        this.OutputChannelCount = outputChannelCount;
        this.GridPointCount = gridPointCount;
        this.CheckValues();
    }

    /// <summary>
    /// Gets the values that make up this table.
    /// </summary>
    public float[] Values { get; }

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

        return this.EqualsValuesArray(other)
            && this.DataType == other.DataType
            && this.InputChannelCount == other.InputChannelCount
            && this.OutputChannelCount == other.OutputChannelCount
            && this.GridPointCount.AsSpan().SequenceEqual(other.GridPointCount);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IccClut other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
            this.Values,
            this.DataType,
            this.InputChannelCount,
            this.OutputChannelCount,
            this.GridPointCount);

    private bool EqualsValuesArray(IccClut other)
    {
        if (this.Values.Length != other.Values.Length)
        {
            return false;
        }

        return this.Values.SequenceEqual(other.Values);
    }

    private void CheckValues()
    {
        Guard.MustBeBetweenOrEqualTo(this.InputChannelCount, 1, 15, nameof(this.InputChannelCount));
        Guard.MustBeBetweenOrEqualTo(this.OutputChannelCount, 1, 15, nameof(this.OutputChannelCount));

        int length = 0;
        for (int i = 0; i < this.InputChannelCount; i++)
        {
            length += (int)Math.Pow(this.GridPointCount[i], this.InputChannelCount);
        }

        // TODO: Disabled this check, not sure if this check is correct.
        // Guard.IsTrue(this.Values.Length == length, nameof(this.Values), "Length of values array does not match the grid points");
    }
}
