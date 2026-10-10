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
        public override void FromVector4Destructive(
            Configuration configuration,
            Span<Vector4> sourceVectors,
            Span<RgbaVector> destinationPixels,
            PixelConversionModifiers modifiers)
        {
            Guard.DestinationShouldNotBeTooShort(sourceVectors, destinationPixels, nameof(destinationPixels));

            Vector4Converters.ApplyBackwardConversionModifiers(sourceVectors, modifiers);

            MemoryMarshal.Cast<Vector4, RgbaVector>(sourceVectors).CopyTo(destinationPixels[..sourceVectors.Length]);
        }

        /// <inheritdoc />
        public override void ToVector4(
            Configuration configuration,
            ReadOnlySpan<RgbaVector> sourcePixels,
            Span<Vector4> destinationVectors,
            PixelConversionModifiers modifiers)
        {
            Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationVectors, nameof(destinationVectors));

            destinationVectors = destinationVectors[..sourcePixels.Length];
            MemoryMarshal.Cast<RgbaVector, Vector4>(sourcePixels).CopyTo(destinationVectors);

            Vector4Converters.ApplyForwardConversionModifiers(destinationVectors, modifiers);
        }
    }
}
