// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Implements the 'WhiteIsZero' photometric interpretation (optimized for 8-bit grayscale images).
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class WhiteIsZero8TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhiteIsZero8TiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    public WhiteIsZero8TiffColor(Configuration configuration) => this.configuration = configuration;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        // WhiteIsZero stores white as 0 and black as 255; complementing each byte produces ordinary L8 samples.
        // Sixty-four bytes allow a full Vector512 operation, two Vector256 operations, or four Vector128 operations.
        // Reuse this block across rows without changing the source or renting a temporary row.
        InlineArray64<byte> byteBuffer = default;
        Span<byte> inverted = byteBuffer;
        int offset = 0;

        for (int y = top; y < top + height; y++)
        {
            // Encoded samples start at offset zero for this region. left and top locate it in the destination image.
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < width; x += inverted.Length)
            {
                int count = Math.Min(inverted.Length, width - x);

                // The shared operation selects the supported SIMD width and handles short tails and scalar hardware.
                TensorPrimitives.OnesComplement<byte>(data.Slice(offset, count), inverted[..count]);

                // The bulk pixel API owns destination packing and alpha. Only the initialized prefix reaches that API.
                PixelOperations<TPixel>.Instance.FromL8Bytes(this.configuration, inverted[..count], pixelRow.Slice(x, count), count);
                offset += count;
            }
        }
    }
}
