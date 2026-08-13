// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct NormalizedByte2
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal class PixelOperations : PixelOperations<NormalizedByte2>
    {
        private static readonly Vector4 NativeOffset = new(1F, 1F, 0F, 0F);
        private static readonly Vector4 NativeDivisor = new(2F, 2F, 1F, 1F);

        // Alpha is implicitly one, so both outward representations already contain associated color components.

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte2> source, Span<Vector4> destination) => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte2> source, Span<Vector4> destination) => this.ToUnassociatedScaledVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte2> destination)
        {
            // Only X and Y use affine native coordinates. Preserve W as normalized alpha for the scaled unassociation step.
            Vector4Converters.AddThenDivide(source, NativeOffset, NativeDivisor);
            this.FromAssociatedScaledVector4Destructive(configuration, source, destination);
        }
    }
}
