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
        public override void From<TSourcePixel>(
            Configuration configuration,
            ReadOnlySpan<TSourcePixel> sourcePixels,
            Span<RgbaVector> destinationPixels)
        {
            Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

            destinationPixels = destinationPixels[..sourcePixels.Length];
            Span<Vector4> destinationVectors = MemoryMarshal.Cast<RgbaVector, Vector4>(destinationPixels);

            // Cross-format conversion uses public dispatch so associated source pixels are unassociated before entering RgbaVector storage.
            PixelOperations<TSourcePixel>.Instance.ToVector4(configuration, sourcePixels, destinationVectors, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

            // RgbaVector.FromScaledVector4 clamps scaled input, so the optimized bulk path must preserve that behavior after unassociating.
            Numerics.Clamp(MemoryMarshal.Cast<Vector4, float>(destinationVectors), 0F, 1F);
        }

        /// <inheritdoc />
        public override void FromVector4Destructive(
            Configuration configuration,
            Span<Vector4> sourceVectors,
            Span<RgbaVector> destinationPixels,
            PixelConversionModifiers modifiers)
        {
            Guard.DestinationShouldNotBeTooShort(sourceVectors, destinationPixels, nameof(destinationPixels));

            Vector4Converters.ApplyBackwardConversionModifiers(sourceVectors, modifiers);

            // RgbaVector.FromVector4 and FromScaledVector4 both clamp to the representable [0, 1] range. Preserve that scalar
            // contract before the zero-copy representation cast so bulk processor output cannot retain HDR or negative values.
            Numerics.Clamp(MemoryMarshal.Cast<Vector4, float>(sourceVectors), 0F, 1F);
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
