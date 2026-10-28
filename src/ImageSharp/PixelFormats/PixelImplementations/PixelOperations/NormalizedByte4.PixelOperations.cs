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
        private static readonly Vector4 NativeScale = new(2F);

        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // The layouts contain the same signed byte codes. This overload only unpacks storage; it does not apply alpha conversion.
            NormalizedByte4P.PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source), destination[..source.Length], false);
        }

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // The packed P and non-P layouts are identical, so the shared SIMD unpacker supplies normalized values. Association
            // happens in [0, 1] before the result is mapped back to the signed-native [-1, 1] coordinate system.
            NormalizedByte4P.PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source), destination, true);
            Numerics.Premultiply(destination);
            PixelFormats.PixelOperations.MultiplyThenAdd(destination, NativeScale, -Vector4.One);
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source), destination[..source.Length], true);
        }

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            NormalizedByte4P.PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source), destination, true);
            Numerics.Premultiply(destination);
        }

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Share the signed-byte packer because association is already resolved by this entry point.
            NormalizedByte4P.PixelOperations.Pack(source, MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination), false);
        }

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Native RGB and alpha use the same affine encoding. Decode them before the shared alpha conversion.
            PixelFormats.PixelOperations.AddThenDivide(source, Vector4.One, NativeScale);
            this.FromAssociatedScaledVector4Destructive(configuration, source, destination);
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(
            Configuration configuration,
            Span<Vector4> source,
            Span<NormalizedByte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            NormalizedByte4P.PixelOperations.Pack(source, MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination), true);
        }

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            PixelFormats.PixelOperations.UnPremultiplyForPacking(source);
            NormalizedByte4P.PixelOperations.Pack(source, MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination), true);
        }
    }
}
