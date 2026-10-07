// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

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
        InlineArray16<byte> byteBuffer = default;
        Span<byte> inverted = byteBuffer;
        int offset = 0;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < width; x += inverted.Length)
            {
                int count = Math.Min(inverted.Length, width - x);

                if (Vector128.IsHardwareAccelerated && count == inverted.Length)
                {
                    // Complement all sixteen encoded bytes before FromL8 applies the target pixel's exact L8 mapping.
                    Vector128<byte> source = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(data), (nuint)offset);
                    (Vector128.Create(byte.MaxValue) - source).StoreUnsafe(ref MemoryMarshal.GetReference(inverted));
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        inverted[i] = (byte)(byte.MaxValue - data[offset + i]);
                    }
                }

                PixelOperations<TPixel>.Instance.FromL8Bytes(this.configuration, inverted[..count], pixelRow.Slice(x, count), count);
                offset += count;
            }
        }
    }
}
