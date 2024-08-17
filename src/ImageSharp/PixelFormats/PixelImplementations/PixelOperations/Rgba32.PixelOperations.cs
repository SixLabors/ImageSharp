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
        public override void ToVector4(
            Configuration configuration,
            ReadOnlySpan<Rgba32> source,
            Span<Vector4> destinationVectors,
            PixelConversionModifiers modifiers)
        {
            Guard.DestinationShouldNotBeTooShort(source, destinationVectors, nameof(destinationVectors));

            destinationVectors = destinationVectors[..source.Length];
            SimdUtils.ByteToNormalizedFloat(
                MemoryMarshal.Cast<Rgba32, byte>(source),
                MemoryMarshal.Cast<Vector4, float>(destinationVectors));
            Vector4Converters.ApplyForwardConversionModifiers(destinationVectors, modifiers);
        }

        /// <inheritdoc />
        public override void FromVector4Destructive(
            Configuration configuration,
            Span<Vector4> sourceVectors,
            Span<Rgba32> destination,
            PixelConversionModifiers modifiers)
        {
            Guard.DestinationShouldNotBeTooShort(sourceVectors, destination, nameof(destination));

            destination = destination[..sourceVectors.Length];
            Vector4Converters.ApplyBackwardConversionModifiers(sourceVectors, modifiers);
            SimdUtils.NormalizedFloatToByteSaturate(
                MemoryMarshal.Cast<Vector4, float>(sourceVectors),
                MemoryMarshal.Cast<Rgba32, byte>(destination));
        }

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
    }
}
