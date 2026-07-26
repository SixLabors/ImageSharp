// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct Rgb48
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal partial class PixelOperations : PixelOperations<Rgb48>
    {
        // Alpha is implicitly one, so both outward representations already contain associated color components.

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<Vector4> destination) => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<Vector4> destination) => this.ToUnassociatedScaledVector4(configuration, source, destination);
    }
}
