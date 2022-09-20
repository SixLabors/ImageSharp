// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// This is a simple text structure that contains a text string.
/// </summary>
internal sealed class IccTextTagDataEntry : IccTagDataEntry, IEquatable<IccTextTagDataEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccTextTagDataEntry"/> class.
    /// </summary>
    /// <param name="text">The Text</param>
    public IccTextTagDataEntry(string text)
        : this(text, IccProfileTag.Unknown)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccTextTagDataEntry"/> class.
    /// </summary>
    /// <param name="text">The Text</param>
    /// <param name="tagSignature">Tag Signature</param>
    public IccTextTagDataEntry(string text, IccProfileTag tagSignature)
        : base(IccTypeSignature.Text, tagSignature)
        => this.Text = text ?? throw new ArgumentNullException(nameof(text));

    /// <summary>
    /// Gets the Text
    /// </summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override bool Equals(IccTagDataEntry? other)
        => other is IccTextTagDataEntry entry && this.Equals(entry);

    /// <inheritdoc />
    public bool Equals(IccTextTagDataEntry? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return base.Equals(other) && string.Equals(this.Text, other.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is IccTextTagDataEntry other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(this.Signature, this.Text);
}
