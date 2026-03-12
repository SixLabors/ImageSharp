// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct Rgb24
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal partial class PixelOperations : PixelOperations<Rgb24>
    {
        /// <inheritdoc />
        internal override void PackFromRgbPlanes(
            ReadOnlySpan<byte> redChannel,
            ReadOnlySpan<byte> greenChannel,
            ReadOnlySpan<byte> blueChannel,
            Span<Rgb24> destination)
        {
            int count = redChannel.Length;
            GuardPackFromRgbPlanes(greenChannel, blueChannel, destination, count);

            SimdUtils.PackFromRgbPlanes(redChannel, greenChannel, blueChannel, destination);
        }

        /// <inheritdoc />
        internal override void UnpackIntoRgbPlanes(
           Span<float> redChannel,
           Span<float> greenChannel,
           Span<float> blueChannel,
           ReadOnlySpan<Rgb24> source)
        {
            GuardUnpackIntoRgbPlanes(redChannel, greenChannel, blueChannel, source);
            SimdUtils.UnpackToRgbPlanes(redChannel, greenChannel, blueChannel, source);
        }
    }
}
