// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct L8
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal partial class PixelOperations : PixelOperations<L8>
    {
        // Alpha is implicitly one, so both outward representations already contain associated color components.

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<L8> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            this.ToUnassociatedVector4(configuration, source, destination);
        }

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<L8> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            this.ToUnassociatedScaledVector4(configuration, source, destination);
        }
    }
}
