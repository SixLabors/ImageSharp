// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// A segment of a curve
/// </summary>
internal abstract class IccCurveSegment : IEquatable<IccCurveSegment>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccCurveSegment"/> class.
    /// </summary>
    /// <param name="signature">The signature of this segment</param>
    protected IccCurveSegment(IccCurveSegmentSignature signature)
        => this.Signature = signature;

    /// <summary>
    /// Gets the signature of this segment
    /// </summary>
    public IccCurveSegmentSignature Signature { get; }

    /// <inheritdoc/>
    public virtual bool Equals(IccCurveSegment? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return this.Signature == other.Signature;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => this.Equals(obj as IccCurveSegment);

    /// <inheritdoc/>
    public override int GetHashCode() => this.Signature.GetHashCode();
}
