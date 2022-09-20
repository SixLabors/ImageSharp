// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Lookup Table
/// </summary>
internal readonly struct IccLut : IEquatable<IccLut>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccLut"/> struct.
    /// </summary>
    /// <param name="values">The LUT values</param>
    public IccLut(float[] values)
        => this.Values = values ?? throw new ArgumentNullException(nameof(values));

    /// <summary>
    /// Initializes a new instance of the <see cref="IccLut"/> struct.
    /// </summary>
    /// <param name="values">The LUT values</param>
    public IccLut(ushort[] values)
    {
        Guard.NotNull(values, nameof(values));

        const float max = ushort.MaxValue;

        this.Values = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            this.Values[i] = values[i] / max;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccLut"/> struct.
    /// </summary>
    /// <param name="values">The LUT values</param>
    public IccLut(byte[] values)
    {
        Guard.NotNull(values, nameof(values));

        const float max = byte.MaxValue;

        this.Values = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            this.Values[i] = values[i] / max;
        }
    }

    /// <summary>
    /// Gets the values that make up this table
    /// </summary>
    public float[] Values { get; }

    /// <inheritdoc/>
    public bool Equals(IccLut other)
    {
        if (ReferenceEquals(this.Values, other.Values))
        {
            return true;
        }

        return this.Values.AsSpan().SequenceEqual(other.Values);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is IccLut iccLut && this.Equals(iccLut);

    /// <inheritdoc/>
    public override int GetHashCode()
        => this.Values.GetHashCode();
}
