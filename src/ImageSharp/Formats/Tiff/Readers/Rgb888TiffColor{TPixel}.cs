// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Implements the 'RGB' photometric interpretation (optimized for 8-bit full color images).
/// </summary>
internal class Rgb888TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;

    public Rgb888TiffColor(Configuration configuration) => this.configuration = configuration;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;
        Span<Vector4> vectors = this.GetColorBuffer(this.configuration, width, []);

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            int byteCount = pixelRow.Length * 3;
            ReadOnlySpan<Rgb24> samples = MemoryMarshal.Cast<byte, Rgb24>(data.Slice(offset, byteCount));

            if (!this.TryStoreColorPixels(this.configuration, samples, vectors, 0))
            {
                PixelOperations<TPixel>.Instance.FromRgb24Bytes(this.configuration, data.Slice(offset, byteCount), pixelRow, pixelRow.Length);
            }

            this.CompleteColorRow(this.configuration, vectors, pixelRow, PixelAlphaRepresentation.None);

            offset += byteCount;
        }
    }
}
