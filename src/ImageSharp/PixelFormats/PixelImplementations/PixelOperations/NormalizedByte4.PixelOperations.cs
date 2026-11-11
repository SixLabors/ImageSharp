// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct NormalizedByte4
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal class PixelOperations : PixelOperations<NormalizedByte4>
    {
        // The packed P and non-P layouts contain the same signed byte codes, so both formats share the signed-byte kernels.
        // The kernels associate and unassociate scaled vectors, and map native vectors to and from the scaled range.

        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.ToVector4<PixelFormats.PixelOperations.NoAlphaOperator>(
                MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source),
                destination[..source.Length],
                false);
        }

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.ToVector4<PixelFormats.PixelOperations.AssociateOperator>(
                MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source),
                destination[..source.Length],
                false);
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.ToVector4<PixelFormats.PixelOperations.NoAlphaOperator>(
                MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source),
                destination[..source.Length],
                true);
        }

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.ToVector4<PixelFormats.PixelOperations.AssociateOperator>(
                MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source),
                destination[..source.Length],
                true);
        }

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.Pack<PixelFormats.PixelOperations.NoAlphaOperator>(
                source,
                MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination[..source.Length]),
                false);
        }

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.Pack<PixelFormats.PixelOperations.UnassociateOperator>(
                source,
                MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination[..source.Length]),
                false);
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(
            Configuration configuration,
            Span<Vector4> source,
            Span<NormalizedByte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.Pack<PixelFormats.PixelOperations.NoAlphaOperator>(
                source,
                MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination[..source.Length]),
                true);
        }

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.Pack<PixelFormats.PixelOperations.UnassociateOperator>(
                source,
                MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination[..source.Length]),
                true);
        }
    }
}
