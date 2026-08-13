// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct RgbaHalf
{
    /// <summary>
    /// Provides optimized bulk operations for <see cref="RgbaHalf"/>.
    /// </summary>
    internal class PixelOperations : PixelOperations<RgbaHalf>
    {
        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<RgbaHalf> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // RgbaHalf and RgbaHalfP have the same four-half layout. Sharing the binary16 expansion kernel keeps this path
            // vectorized without changing the unassociated meaning of the source components.
            RgbaHalfP.PixelOperations.Unpack(MemoryMarshal.Cast<RgbaHalf, RgbaHalfP>(source), destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<RgbaHalf> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Association is fused with half expansion so the conversion reads and writes each span once.
            RgbaHalfP.PixelOperations.UnpackAssociated(MemoryMarshal.Cast<RgbaHalf, RgbaHalfP>(source), destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaHalf> source, Span<Vector4> destination)
            => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaHalf> source, Span<Vector4> destination)
            => this.ToAssociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalf> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // The layouts are identical, so the shared packer can write RgbaHalf storage without an intermediate buffer.
            RgbaHalfP.PixelOperations.Pack(source, MemoryMarshal.Cast<RgbaHalf, RgbaHalfP>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalf> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Unassociation is fused with binary16 packing so processors do not pay for another pass over their vector buffer.
            RgbaHalfP.PixelOperations.PackFromAssociated(source, MemoryMarshal.Cast<RgbaHalf, RgbaHalfP>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalf> destination)
            => this.FromUnassociatedVector4Destructive(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalf> destination)
            => this.FromAssociatedVector4Destructive(configuration, source, destination);
    }
}
