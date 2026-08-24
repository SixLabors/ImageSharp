// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct Short2
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal class PixelOperations : PixelOperations<Short2>
    {
        private static readonly Vector4 NativeOffset = new(-MinNeg, -MinNeg, 0F, 0F);
        private static readonly Vector4 NativeRange = new(Range, Range, 1F, 1F);

        // Alpha is implicitly one, so both outward representations already contain associated color components.

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Short2> source, Span<Vector4> destination) => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Short2> source, Span<Vector4> destination) => this.ToUnassociatedScaledVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Short2> destination)
        {
            // X and Y use signed-native coordinates while incoming W remains normalized alpha. Map only the stored components before
            // the scaled bulk path unassociates RGB; transforming W would change the opacity that must be removed.
            Vector4Converters.AddThenDivide(source, NativeOffset, NativeRange);
            this.FromAssociatedScaledVector4Destructive(configuration, source, destination);
        }
    }
}
