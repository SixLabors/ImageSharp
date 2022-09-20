﻿// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// This type is an array of structures, each of which contains information
/// for identification of a profile used in a sequence.
/// </summary>
internal sealed class IccProfileSequenceIdentifierTagDataEntry : IccTagDataEntry, IEquatable<IccProfileSequenceIdentifierTagDataEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccProfileSequenceIdentifierTagDataEntry"/> class.
    /// </summary>
    /// <param name="data">Profile Identifiers</param>
    public IccProfileSequenceIdentifierTagDataEntry(IccProfileSequenceIdentifier[] data)
        : this(data, IccProfileTag.Unknown)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccProfileSequenceIdentifierTagDataEntry"/> class.
    /// </summary>
    /// <param name="data">Profile Identifiers</param>
    /// <param name="tagSignature">Tag Signature</param>
    public IccProfileSequenceIdentifierTagDataEntry(IccProfileSequenceIdentifier[] data, IccProfileTag tagSignature)
        : base(IccTypeSignature.ProfileSequenceIdentifier, tagSignature)
    {
        this.Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Gets the profile identifiers
    /// </summary>
    public IccProfileSequenceIdentifier[] Data { get; }

    /// <inheritdoc/>
    public override bool Equals(IccTagDataEntry? other)
    {
        return other is IccProfileSequenceIdentifierTagDataEntry entry && this.Equals(entry);
    }

    /// <inheritdoc />
    public bool Equals(IccProfileSequenceIdentifierTagDataEntry? other)
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is IccProfileSequenceIdentifierTagDataEntry other && this.Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(this.Signature, this.Data);
}
