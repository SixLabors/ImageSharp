// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// A CLUT (color lookup table) element to process data
/// </summary>
internal sealed class IccClutProcessElement : IccMultiProcessElement, IEquatable<IccClutProcessElement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccClutProcessElement"/> class.
    /// </summary>
    /// <param name="clutValue">The color lookup table of this element</param>
    public IccClutProcessElement(IccClut clutValue)
        : base(IccMultiProcessElementSignature.Clut, clutValue?.InputChannelCount ?? 1, clutValue?.OutputChannelCount ?? 1)
        => this.ClutValue = clutValue ?? throw new ArgumentNullException(nameof(clutValue));

    /// <summary>
    /// Gets the color lookup table of this element
    /// </summary>
    public IccClut ClutValue { get; }

    /// <inheritdoc />
    public override bool Equals(IccMultiProcessElement other)
    {
        if (base.Equals(other) && other is IccClutProcessElement element)
        {
            return this.ClutValue.Equals(element.ClutValue);
        }

        return false;
    }

    /// <inheritdoc />
    public bool Equals(IccClutProcessElement other) => this.Equals((IccMultiProcessElement)other);

    /// <inheritdoc />
    public override bool Equals(object obj) => this.Equals(obj as IccClutProcessElement);

    /// <inheritdoc />
    public override int GetHashCode() => base.GetHashCode();
}
