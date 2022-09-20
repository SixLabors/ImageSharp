﻿// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// The purpose of this tag is to identify the colorants used in
/// the profile by a unique name and set of PCSXYZ or PCSLAB values
/// to give the colorant an unambiguous value.
/// </summary>
internal sealed class IccColorantTableTagDataEntry : IccTagDataEntry, IEquatable<IccColorantTableTagDataEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccColorantTableTagDataEntry"/> class.
    /// </summary>
    /// <param name="colorantData">Colorant Data</param>
    public IccColorantTableTagDataEntry(IccColorantTableEntry[] colorantData)
        : this(colorantData, IccProfileTag.Unknown)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccColorantTableTagDataEntry"/> class.
    /// </summary>
    /// <param name="colorantData">Colorant Data</param>
    /// <param name="tagSignature">Tag Signature</param>
    public IccColorantTableTagDataEntry(IccColorantTableEntry[] colorantData, IccProfileTag tagSignature)
        : base(IccTypeSignature.ColorantTable, tagSignature)
    {
        Guard.NotNull(colorantData, nameof(colorantData));
        Guard.MustBeBetweenOrEqualTo(colorantData.Length, 1, 15, nameof(colorantData));

        this.ColorantData = colorantData;
    }

    /// <summary>
    /// Gets the colorant data
    /// </summary>
    public IccColorantTableEntry[] ColorantData { get; }

    /// <inheritdoc/>
    public override bool Equals(IccTagDataEntry? other) => other is IccColorantTableTagDataEntry entry && this.Equals(entry);

    /// <inheritdoc/>
    public bool Equals(IccColorantTableTagDataEntry? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return base.Equals(other) && this.ColorantData.AsSpan().SequenceEqual(other.ColorantData);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IccColorantTableTagDataEntry other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Signature, this.ColorantData);
}
