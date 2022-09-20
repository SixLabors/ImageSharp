// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// This type contains curves representing the under color removal and black generation
/// and a text string which is a general description of the method used for the UCR and BG.
/// </summary>
internal sealed class IccUcrBgTagDataEntry : IccTagDataEntry, IEquatable<IccUcrBgTagDataEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccUcrBgTagDataEntry"/> class.
    /// </summary>
    /// <param name="ucrCurve">UCR (under color removal) curve values</param>
    /// <param name="bgCurve">BG (black generation) curve values</param>
    /// <param name="description">Description of the used UCR and BG method</param>
    public IccUcrBgTagDataEntry(ushort[] ucrCurve, ushort[] bgCurve, string description)
        : this(ucrCurve, bgCurve, description, IccProfileTag.Unknown)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccUcrBgTagDataEntry"/> class.
    /// </summary>
    /// <param name="ucrCurve">UCR (under color removal) curve values</param>
    /// <param name="bgCurve">BG (black generation) curve values</param>
    /// <param name="description">Description of the used UCR and BG method</param>
    /// <param name="tagSignature">Tag Signature</param>
    public IccUcrBgTagDataEntry(ushort[] ucrCurve, ushort[] bgCurve, string description, IccProfileTag tagSignature)
        : base(IccTypeSignature.UcrBg, tagSignature)
    {
        this.UcrCurve = ucrCurve ?? throw new ArgumentNullException(nameof(ucrCurve));
        this.BgCurve = bgCurve ?? throw new ArgumentNullException(nameof(bgCurve));
        this.Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    /// <summary>
    /// Gets the UCR (under color removal) curve values
    /// </summary>
    public ushort[] UcrCurve { get; }

    /// <summary>
    /// Gets the BG (black generation) curve values
    /// </summary>
    public ushort[] BgCurve { get; }

    /// <summary>
    /// Gets a description of the used UCR and BG method
    /// </summary>
    public string Description { get; }

    /// <inheritdoc/>
    public override bool Equals(IccTagDataEntry? other)
        => other is IccUcrBgTagDataEntry entry && this.Equals(entry);

    /// <inheritdoc/>
    public bool Equals(IccUcrBgTagDataEntry? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return base.Equals(other)
            && this.UcrCurve.AsSpan().SequenceEqual(other.UcrCurve)
            && this.BgCurve.AsSpan().SequenceEqual(other.BgCurve)
            && string.Equals(this.Description, other.Description, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is IccUcrBgTagDataEntry other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(
            this.Signature,
            this.UcrCurve,
            this.BgCurve,
            this.Description);
}
