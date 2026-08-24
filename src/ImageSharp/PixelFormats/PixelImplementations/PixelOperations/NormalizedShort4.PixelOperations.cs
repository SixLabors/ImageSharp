// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct NormalizedShort4
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal class PixelOperations : PixelOperations<NormalizedShort4>
    {
        private static readonly Vector4 NativeScale = new(2F);

        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedShort4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SignedShort4PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedShort4, short>(source), destination[..source.Length], true, false);
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedShort4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Association is defined in scaled color space, not around the signed-native zero point. The shared signed-short SIMD
            // kernel handles unpacking before the result is mapped back to the format's native [-1, 1] range.
            SignedShort4PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedShort4, short>(source), destination, true, true);
            Numerics.Premultiply(destination);
            Vector4Converters.MultiplyThenAdd(destination, NativeScale, -Vector4.One);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedShort4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SignedShort4PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedShort4, short>(source), destination[..source.Length], true, true);
        }

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedShort4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            SignedShort4PixelOperations.ToVector4(MemoryMarshal.Cast<NormalizedShort4, short>(source), destination, true, true);
            Numerics.Premultiply(destination);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedShort4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            SignedShort4PixelOperations.FromVector4(source, MemoryMarshal.Cast<NormalizedShort4, short>(destination), true, false);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedShort4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Convert native RGB and alpha together before unassociation so the subsequent scaled pack retains the scalar contract.
            Vector4Converters.AddThenDivide(source, Vector4.One, NativeScale);
            Numerics.UnPremultiply(source);
            SignedShort4PixelOperations.FromVector4(source, MemoryMarshal.Cast<NormalizedShort4, short>(destination), true, true);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedShort4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            SignedShort4PixelOperations.FromVector4(source, MemoryMarshal.Cast<NormalizedShort4, short>(destination), true, true);
        }

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedShort4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            Numerics.UnPremultiply(source);
            SignedShort4PixelOperations.FromVector4(source, MemoryMarshal.Cast<NormalizedShort4, short>(destination), true, true);
        }
    }
}
