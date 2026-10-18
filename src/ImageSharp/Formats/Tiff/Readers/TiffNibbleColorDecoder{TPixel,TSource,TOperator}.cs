// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Decodes byte-padded rows of regularly packed four-bit RGB or RGBA samples.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
/// <typeparam name="TSource">The stored nibble layout.</typeparam>
/// <typeparam name="TOperator">The photometric conversion.</typeparam>
internal sealed class TiffNibbleColorDecoder<TPixel, TSource, TOperator> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
    where TSource : struct, ITiffNibbleColorSource
    where TOperator : struct, ITiffColorOperator<float>
{
    private readonly Configuration configuration;
    private readonly TSource source;
    private readonly TOperator colorOperator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffNibbleColorDecoder{TPixel, TSource, TOperator}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="source">The stored nibble layout.</param>
    /// <param name="colorOperator">The photometric conversion.</param>
    public TiffNibbleColorDecoder(Configuration configuration, TSource source, TOperator colorOperator)
    {
        this.configuration = configuration;
        this.source = source;
        this.colorOperator = colorOperator;
    }

    /// <inheritdoc />
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        InlineArray16<Vector4> vectorBuffer = default;
        Span<Vector4> vectors = vectorBuffer;
        int rowByteCount = ((width * this.source.BitsPerPixel) + 7) / 8;
        int rowOffset = 0;

        for (int y = top; y < top + height; y++)
        {
            ReadOnlySpan<byte> row = data.Slice(rowOffset, rowByteCount);
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < width; x += vectors.Length)
            {
                int count = Math.Min(vectors.Length, width - x);
                Span<Vector4> block = vectors[..count];

                // Sixteen pixels fill one Vector512 register per component. The shared traversal also handles
                // Vector256, Vector128, and scalar tails. RGB444 blocks start on a byte boundary because x is even.
                TiffOperatedColorConverter<TSource, TOperator, TiffVector4Store, float, Vector4>.Convert(
                    row[((x * this.source.BitsPerPixel) / 8)..],
                    [],
                    [],
                    [],
                    block,
                    this.source,
                    this.colorOperator,
                    default);

                // The operator has scaled the samples; its alpha representation selects the matching pixel packer.
                switch (this.colorOperator.AlphaRepresentation)
                {
                    case PixelAlphaRepresentation.Associated:
                        PixelOperations<TPixel>.Instance.FromAssociatedScaledVector4Destructive(this.configuration, block, pixelRow.Slice(x, count));
                        break;
                    case PixelAlphaRepresentation.Unassociated:
                        PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, block, pixelRow.Slice(x, count));
                        break;
                    default:
                        PixelOperations<TPixel>.Instance.FromScaledVector4Destructive(this.configuration, block, pixelRow.Slice(x, count));
                        break;
                }
            }

            // RGB444 rows with odd width end in a padding nibble; the next row starts at the next byte.
            rowOffset += rowByteCount;
        }
    }
}
