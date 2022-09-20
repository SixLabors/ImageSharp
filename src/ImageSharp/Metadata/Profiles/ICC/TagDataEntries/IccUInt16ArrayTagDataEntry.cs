﻿// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// This type represents an array of unsigned shorts.
/// </summary>
internal sealed class IccUInt16ArrayTagDataEntry : IccTagDataEntry, IEquatable<IccUInt16ArrayTagDataEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccUInt16ArrayTagDataEntry"/> class.
    /// </summary>
    /// <param name="data">The array data</param>
    public IccUInt16ArrayTagDataEntry(ushort[] data)
        : this(data, IccProfileTag.Unknown)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccUInt16ArrayTagDataEntry"/> class.
    /// </summary>
    /// <param name="data">The array data</param>
    /// <param name="tagSignature">Tag Signature</param>
    public IccUInt16ArrayTagDataEntry(ushort[] data, IccProfileTag tagSignature)
        : base(IccTypeSignature.UInt16Array, tagSignature)
    {
        this.Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Gets the array data
    /// </summary>
    public ushort[] Data { get; }

    /// <inheritdoc/>
    public override bool Equals(IccTagDataEntry? other)
    {
        return other is IccUInt16ArrayTagDataEntry entry && this.Equals(entry);
    }

    /// <inheritdoc/>
    public bool Equals(IccUInt16ArrayTagDataEntry? other)
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
        return obj is IccUInt16ArrayTagDataEntry other && this.Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Signature, this.Data);
}
