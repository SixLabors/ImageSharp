// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats.Utils;

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
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source), destination[..source.Length], false);
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // The packed P and non-P layouts are identical, so the shared SIMD unpacker supplies normalized values. Association
            // happens in [0, 1] before the result is mapped back to the signed-native [-1, 1] coordinate system.
            NormalizedByte4P.PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source), destination, true);
            Numerics.Premultiply(destination);
            Vector4Converters.MultiplyThenAdd(destination, NativeScale, -Vector4.One);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            NormalizedByte4P.PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source), destination[..source.Length], true);
        }

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            NormalizedByte4P.PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(source), destination, true);
            Numerics.Premultiply(destination);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            NormalizedByte4P.PixelOperations.Pack(source, MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination), false);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Convert all native components together so alpha and RGB enter unassociation in the same normalized coordinate system.
            Vector4Converters.AddThenDivide(source, Vector4.One, NativeScale);
            Numerics.UnPremultiply(source);
            NormalizedByte4P.PixelOperations.Pack(source, MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination), true);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            NormalizedByte4P.PixelOperations.Pack(source, MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination), true);
        }

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            Numerics.UnPremultiply(source);
            NormalizedByte4P.PixelOperations.Pack(source, MemoryMarshal.Cast<NormalizedByte4, NormalizedByte4P>(destination), true);
        }
    }
}
