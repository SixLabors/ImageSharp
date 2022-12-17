// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// A placeholder <see cref="IccMultiProcessElement"/> (might be used for future ICC versions)
/// </summary>
internal sealed class IccEAcsProcessElement : IccMultiProcessElement, IEquatable<IccEAcsProcessElement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccEAcsProcessElement"/> class.
    /// </summary>
    /// <param name="inChannelCount">Number of input channels</param>
    /// <param name="outChannelCount">Number of output channels</param>
    public IccEAcsProcessElement(int inChannelCount, int outChannelCount)
        : base(IccMultiProcessElementSignature.EAcs, inChannelCount, outChannelCount)
    {
    }

    /// <inheritdoc />
    public bool Equals(IccEAcsProcessElement other) => base.Equals(other);

    public override bool Equals(object obj) => this.Equals(obj as IccEAcsProcessElement);

    /// <inheritdoc />
    public override int GetHashCode() => base.GetHashCode();
}
