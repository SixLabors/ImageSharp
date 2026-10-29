// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Implements the 'BlackIsZero' photometric interpretation (optimized for 8-bit grayscale images).
/// </summary>
internal class BlackIsZero8TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlackIsZero8TiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The pixel conversion configuration.</param>
    public BlackIsZero8TiffColor(Configuration configuration) => this.configuration = configuration;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        // BlackIsZero byte samples already match L8. Pass each row directly to the bulk pixel API without staging or inversion.
        int offset = 0;
        Span<Vector4> vectors = this.GetColorBuffer(this.configuration, width, []);

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            int byteCount = pixelRow.Length;

            // left and top position this region in the destination; its encoded samples remain contiguous from data[0].
            ReadOnlySpan<L8> samples = MemoryMarshal.Cast<byte, L8>(data.Slice(offset, byteCount));

            if (!this.TryStoreColorPixels(this.configuration, samples, vectors, 0))
            {
                PixelOperations<TPixel>.Instance.FromL8Bytes(this.configuration, data.Slice(offset, byteCount), pixelRow, pixelRow.Length);
            }

            this.CompleteColorRow(this.configuration, vectors, pixelRow, PixelAlphaRepresentation.None);

            offset += byteCount;
        }
    }
}
