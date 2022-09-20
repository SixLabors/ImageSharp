﻿// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// This type represents an array of doubles (from 32bit fixed point values).
/// </summary>
internal sealed class IccFix16ArrayTagDataEntry : IccTagDataEntry, IEquatable<IccFix16ArrayTagDataEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccFix16ArrayTagDataEntry"/> class.
    /// </summary>
    /// <param name="data">The array data</param>
    public IccFix16ArrayTagDataEntry(float[] data)
        : this(data, IccProfileTag.Unknown)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccFix16ArrayTagDataEntry"/> class.
    /// </summary>
    /// <param name="data">The array data</param>
    /// <param name="tagSignature">Tag Signature</param>
    public IccFix16ArrayTagDataEntry(float[] data, IccProfileTag tagSignature)
        : base(IccTypeSignature.S15Fixed16Array, tagSignature)
    {
        this.Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Gets the array data
    /// </summary>
    public float[] Data { get; }

    /// <inheritdoc/>
    public override bool Equals(IccTagDataEntry? other)
    {
        return other is IccFix16ArrayTagDataEntry entry && this.Equals(entry);
    }

    /// <inheritdoc/>
    public bool Equals(IccFix16ArrayTagDataEntry? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return base.Equals(other) && this.Data.AsSpan().SequenceEqual(other.Data);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is IccFix16ArrayTagDataEntry other && this.Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Signature, this.Data);
}
