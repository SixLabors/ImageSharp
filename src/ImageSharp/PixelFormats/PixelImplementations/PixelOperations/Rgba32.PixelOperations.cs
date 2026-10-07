// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct Rgba32
{
    /// <summary>
    /// <see cref="PixelOperations{TPixel}"/> implementation optimized for <see cref="Rgba32"/>.
    /// </summary>
    internal partial class PixelOperations : PixelOperations<Rgba32>
    {
        /// <inheritdoc />
        public override void ToVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destinationVectors)
        {
            Guard.DestinationShouldNotBeTooShort(source, destinationVectors, nameof(destinationVectors));

            destinationVectors = destinationVectors[..source.Length];
            SimdUtils.ByteToNormalizedFloat(
                MemoryMarshal.Cast<Rgba32, byte>(source),
                MemoryMarshal.Cast<Vector4, float>(destinationVectors));
        }

        /// <inheritdoc />
        public override void ToScaledVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destinationVectors)
            => this.ToVector4(configuration, source, destinationVectors);

        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destinationVectors)
            => this.ToVector4(configuration, source, destinationVectors);

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destinationVectors)
        {
            this.ToVector4(configuration, source, destinationVectors);
            Numerics.Premultiply(destinationVectors[..source.Length]);
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destinationVectors)
            => this.ToVector4(configuration, source, destinationVectors);

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destinationVectors)
            => this.ToAssociatedVector4(configuration, source, destinationVectors);

        /// <inheritdoc />
        public override void FromVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<Rgba32> destination)
        {
            Guard.DestinationShouldNotBeTooShort(sourceVectors, destination, nameof(destination));

            destination = destination[..sourceVectors.Length];
            PackFromVector4(sourceVectors, destination);
        }

        /// <inheritdoc />
        public override void FromScaledVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<Rgba32> destination)
            => this.FromVector4Destructive(configuration, sourceVectors, destination);

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<Rgba32> destination)
            => this.FromVector4Destructive(configuration, sourceVectors, destination);

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<Rgba32> destination)
        {
            Guard.DestinationShouldNotBeTooShort(sourceVectors, destination, nameof(destination));

            Numerics.UnPremultiply(sourceVectors);
            PackFromVector4(sourceVectors, destination[..sourceVectors.Length]);
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<Rgba32> destination)
            => this.FromVector4Destructive(configuration, sourceVectors, destination);

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<Rgba32> destination)
            => this.FromAssociatedVector4Destructive(configuration, sourceVectors, destination);

        /// <inheritdoc />
        internal override void PackFromRgbPlanes(
            ReadOnlySpan<byte> redChannel,
            ReadOnlySpan<byte> greenChannel,
            ReadOnlySpan<byte> blueChannel,
            Span<Rgba32> destination)
        {
            int count = redChannel.Length;
            GuardPackFromRgbPlanes(greenChannel, blueChannel, destination, count);

            SimdUtils.PackFromRgbPlanes(redChannel, greenChannel, blueChannel, destination);
        }

        /// <summary>
        /// Converts normalized vector components to saturated RGBA bytes.
        /// </summary>
        /// <param name="sourceVectors">The source vectors.</param>
        /// <param name="destination">The destination pixels.</param>
        private static void PackFromVector4(Span<Vector4> sourceVectors, Span<Rgba32> destination)
            => SimdUtils.NormalizedFloatToByteSaturate(
                MemoryMarshal.Cast<Vector4, float>(sourceVectors),
                MemoryMarshal.Cast<Rgba32, byte>(destination));
    }
}
