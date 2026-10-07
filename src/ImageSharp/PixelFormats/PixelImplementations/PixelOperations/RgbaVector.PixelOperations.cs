// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct RgbaVector
{
    /// <summary>
    /// <see cref="PixelOperations{TPixel}"/> implementation optimized for <see cref="RgbaVector"/>.
    /// </summary>
    internal class PixelOperations : PixelOperations<RgbaVector>
    {
        /// <inheritdoc />
        public override void FromVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<RgbaVector> destinationPixels)
        {
            Guard.DestinationShouldNotBeTooShort(sourceVectors, destinationPixels, nameof(destinationPixels));

            CopyFromVector4(sourceVectors, destinationPixels);
        }

        /// <inheritdoc />
        public override void FromScaledVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<RgbaVector> destinationPixels)
            => this.FromVector4Destructive(configuration, sourceVectors, destinationPixels);

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<RgbaVector> destinationPixels)
            => this.FromVector4Destructive(configuration, sourceVectors, destinationPixels);

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<RgbaVector> destinationPixels)
        {
            Guard.DestinationShouldNotBeTooShort(sourceVectors, destinationPixels, nameof(destinationPixels));

            Numerics.UnPremultiply(sourceVectors);
            CopyFromVector4(sourceVectors, destinationPixels);
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(
            Configuration configuration,
            Span<Vector4> sourceVectors,
            Span<RgbaVector> destinationPixels)
            => this.FromVector4Destructive(configuration, sourceVectors, destinationPixels);

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(
            Configuration configuration,
            Span<Vector4> sourceVectors,
            Span<RgbaVector> destinationPixels)
            => this.FromAssociatedVector4Destructive(configuration, sourceVectors, destinationPixels);

        /// <inheritdoc />
        public override void ToVector4(Configuration configuration, ReadOnlySpan<RgbaVector> sourcePixels, Span<Vector4> destinationVectors)
        {
            Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationVectors, nameof(destinationVectors));

            destinationVectors = destinationVectors[..sourcePixels.Length];
            MemoryMarshal.Cast<RgbaVector, Vector4>(sourcePixels).CopyTo(destinationVectors);
        }

        /// <inheritdoc />
        public override void ToScaledVector4(Configuration configuration, ReadOnlySpan<RgbaVector> sourcePixels, Span<Vector4> destinationVectors)
            => this.ToVector4(configuration, sourcePixels, destinationVectors);

        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<RgbaVector> sourcePixels, Span<Vector4> destinationVectors)
            => this.ToVector4(configuration, sourcePixels, destinationVectors);

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<RgbaVector> sourcePixels, Span<Vector4> destinationVectors)
        {
            this.ToVector4(configuration, sourcePixels, destinationVectors);
            Numerics.Premultiply(destinationVectors[..sourcePixels.Length]);
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaVector> sourcePixels, Span<Vector4> destinationVectors)
            => this.ToVector4(configuration, sourcePixels, destinationVectors);

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaVector> sourcePixels, Span<Vector4> destinationVectors)
            => this.ToAssociatedVector4(configuration, sourcePixels, destinationVectors);

        /// <summary>
        /// Copies the vector components into the corresponding pixel storage.
        /// </summary>
        /// <param name="sourceVectors">The source vectors.</param>
        /// <param name="destinationPixels">The destination pixels.</param>
        private static void CopyFromVector4(Span<Vector4> sourceVectors, Span<RgbaVector> destinationPixels)
            => MemoryMarshal.Cast<Vector4, RgbaVector>(sourceVectors).CopyTo(destinationPixels[..sourceVectors.Length]);
    }
}
