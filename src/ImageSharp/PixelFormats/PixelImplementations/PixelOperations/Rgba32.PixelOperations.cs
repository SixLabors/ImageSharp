// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats
{
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
            private static readonly Lazy<PixelTypeInfo> LazyInfo =
                new Lazy<PixelTypeInfo>(() => PixelTypeInfo.Create<Rgba32>(PixelAlphaRepresentation.Unassociated), true);

            /// <inheritdoc />
            public override PixelTypeInfo GetPixelTypeInfo() => LazyInfo.Value;

            /// <inheritdoc />
            public override void ToVector4(
                Configuration configuration,
                ReadOnlySpan<Rgba32> sourcePixels,
                Span<Vector4> destinationVectors,
                PixelConversionModifiers modifiers)
            {
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationVectors, nameof(destinationVectors));

                destinationVectors = destinationVectors.Slice(0, sourcePixels.Length);
                SimdUtils.ByteToNormalizedFloat(
                    MemoryMarshal.Cast<Rgba32, byte>(sourcePixels),
                    MemoryMarshal.Cast<Vector4, float>(destinationVectors));
                Vector4Converters.ApplyForwardConversionModifiers(destinationVectors, modifiers);
            }

            /// <inheritdoc />
            public override void FromVector4Destructive(
                Configuration configuration,
                Span<Vector4> sourceVectors,
                Span<Rgba32> destinationPixels,
                PixelConversionModifiers modifiers)
            {
                Guard.DestinationShouldNotBeTooShort(sourceVectors, destinationPixels, nameof(destinationPixels));

                destinationPixels = destinationPixels.Slice(0, sourceVectors.Length);
                Vector4Converters.ApplyBackwardConversionModifiers(sourceVectors, modifiers);
                SimdUtils.NormalizedFloatToByteSaturate(
                    MemoryMarshal.Cast<Vector4, float>(sourceVectors),
                    MemoryMarshal.Cast<Rgba32, byte>(destinationPixels));
            }

            /// <inheritdoc />
            internal override void PackFromRgbPlanes(
                Configuration configuration,
                ReadOnlySpan<byte> redChannel,
                ReadOnlySpan<byte> greenChannel,
                ReadOnlySpan<byte> blueChannel,
                Span<Rgba32> destination)
            {
                Guard.NotNull(configuration, nameof(configuration));
                int count = redChannel.Length;
                Guard.IsTrue(greenChannel.Length == count, nameof(greenChannel), "Channels must be of same size!");
                Guard.IsTrue(blueChannel.Length == count, nameof(blueChannel), "Channels must be of same size!");
                Guard.IsTrue(destination.Length > count, nameof(destination), "'destination' span should not be shorter than the source channels!");

                SimdUtils.PackFromRgbPlanes(configuration, redChannel, greenChannel, blueChannel, destination);
            }
        }
    }
}
