// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Reads unsigned 16-bit grayscale samples through the bulk pixel API paired with TiffGrayL16Writer.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
internal sealed class TiffGrayL16Reader<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool isBigEndian;
    private readonly bool whiteIsZero;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffGrayL16Reader{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="isBigEndian">Whether the stored samples use big-endian byte order.</param>
    /// <param name="whiteIsZero">Whether zero represents white.</param>
    public TiffGrayL16Reader(Configuration configuration, bool isBigEndian, bool whiteIsZero)
    {
        this.configuration = configuration;
        this.isBigEndian = isBigEndian;
        this.whiteIsZero = whiteIsZero;
    }

    /// <inheritdoc />
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;
        int bytesPerRow = width * sizeof(ushort);
        Span<Vector4> vectors = this.GetColorBuffer(this.configuration, width, []);

        // Little-endian BlackIsZero samples already match L16 storage. Convert directly without staging or another sample pass.
        if (!this.isBigEndian && !this.whiteIsZero)
        {
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                ReadOnlySpan<L16> samples = MemoryMarshal.Cast<byte, L16>(data.Slice(offset, bytesPerRow));

                if (!this.TryStoreColorPixels(this.configuration, samples, vectors, 0))
                {
                    PixelOperations<TPixel>.Instance.FromL16Bytes(this.configuration, data.Slice(offset, bytesPerRow), pixelRow, width);
                }

                this.CompleteColorRow(this.configuration, vectors, pixelRow, PixelAlphaRepresentation.None);
                offset += bytesPerRow;
            }

            return;
        }

        // TIFF byte order and WhiteIsZero inversion belong to the stored samples. Combine them before pixel conversion.
        // Thirty-two ushort samples fill a Vector512 register. Reuse this block across rows and keep source bytes unchanged.
        InlineArray32<L16> storage = default;
        Span<L16> block = storage;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < width; x += block.Length)
            {
                int count = Math.Min(width - x, block.Length);
                Span<L16> samples = block[..count];

                // The helper reads complete ushort samples and applies 65535 - sample for WhiteIsZero.
                // It also handles the final short block, so no row padding or reads beyond the encoded samples are needed.
                TiffUtilities.CopyUShorts(
                    data.Slice(offset + (x * sizeof(ushort)), count * sizeof(ushort)),
                    MemoryMarshal.Cast<L16, ushort>(samples),
                    this.isBigEndian,
                    this.whiteIsZero);

                // L16 now has ordinary black-is-zero values. The existing bulk conversion owns destination precision and alpha.
                if (!this.TryStoreColorPixels(this.configuration, (ReadOnlySpan<L16>)samples, vectors, x))
                {
                    PixelOperations<TPixel>.Instance.FromL16(this.configuration, samples, pixelRow.Slice(x, count));
                }
            }

            this.CompleteColorRow(this.configuration, vectors, pixelRow, PixelAlphaRepresentation.None);

            offset += bytesPerRow;
        }
    }
}
