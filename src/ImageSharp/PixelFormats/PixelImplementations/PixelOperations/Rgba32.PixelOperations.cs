// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct Rgba32
{
    /// <summary>
    /// <see cref="PixelOperations{TPixel}"/> implementation optimized for <see cref="Rgba32"/>.
    /// </summary>
    internal partial class PixelOperations : RgbaCompatiblePixelOperations<Rgba32>
    {
        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // RGBA already has vector component order. Widen directly without staging or channel shuffles.
            SimdUtils.ByteToNormalizedFloat(MemoryMarshal.AsBytes(source), MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destination)
        {
            // Canonical RGBA needs no staging threshold. Retain bulk normalization before association at every row length.
            this.ToUnassociatedVector4(configuration, source, destination);
            Numerics.Premultiply(destination[..source.Length]);
        }

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Canonical RGBA output needs neither a temporary row nor a typed shuffle after byte packing.
            SimdUtils.NormalizedFloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), MemoryMarshal.AsBytes(destination[..source.Length]));
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
