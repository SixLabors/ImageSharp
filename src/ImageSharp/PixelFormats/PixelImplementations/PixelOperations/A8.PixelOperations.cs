// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct A8
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal class PixelOperations : PixelOperations<A8>
    {
        // A8 stores alpha without color components, so association cannot alter any emitted or consumed vector component.

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<A8> source, Span<Vector4> destination) => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<A8> source, Span<Vector4> destination) => this.ToUnassociatedScaledVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<A8> destination) => this.FromUnassociatedVector4Destructive(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<A8> destination) => this.FromUnassociatedScaledVector4Destructive(configuration, source, destination);
    }
}
