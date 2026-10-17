// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Decodes separate TIFF component planes through a closed source and photometric operator.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
/// <typeparam name="TSource">The source sample layout.</typeparam>
/// <typeparam name="TOperator">The photometric conversion.</typeparam>
internal sealed class TiffPlanarColorDecoder<TPixel, TSource, TOperator> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
    where TSource : struct, ITiffColorSource<float>
    where TOperator : struct, ITiffColorOperator<float>
{
    private readonly Configuration configuration;
    private readonly TSource source;
    private readonly TOperator colorOperator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffPlanarColorDecoder{TPixel, TSource, TOperator}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="source">The planar sample reader.</param>
    /// <param name="colorOperator">The photometric conversion.</param>
    public TiffPlanarColorDecoder(Configuration configuration, TSource source, TOperator colorOperator)
    {
        this.configuration = configuration;
        this.source = source;
        this.colorOperator = colorOperator;
    }

    /// <inheritdoc />
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        InlineArray16<Vector4> vectorBuffer = default;
        Span<Vector4> vectors = vectorBuffer;
        ReadOnlySpan<byte> first = data[0].GetSpan();
        ReadOnlySpan<byte> second = data[1].GetSpan();
        ReadOnlySpan<byte> third = data[2].GetSpan();
        ReadOnlySpan<byte> fourth = this.source.ComponentCount == 4 ? data[3].GetSpan() : [];
        int rowOffset = 0;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < width; x += vectors.Length)
            {
                int count = Math.Min(vectors.Length, width - x);
                int byteOffset = rowOffset + (x * this.source.BytesPerPixel);
                int byteCount = count * this.source.BytesPerPixel;
                Span<Vector4> block = vectors[..count];

                // Each source span begins at the same pixel in its own plane. The register
                // traversal receives complete bounded groups and writes this block only once.
                TiffOperatedColorConverter<TSource, TOperator, TiffVector4Store, float, Vector4>.Convert(
                    first.Slice(byteOffset, byteCount),
                    second.Slice(byteOffset, byteCount),
                    third.Slice(byteOffset, byteCount),
                    this.source.ComponentCount == 4 ? fourth.Slice(byteOffset, byteCount) : [],
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

            rowOffset += width * this.source.BytesPerPixel;
        }
    }
}
