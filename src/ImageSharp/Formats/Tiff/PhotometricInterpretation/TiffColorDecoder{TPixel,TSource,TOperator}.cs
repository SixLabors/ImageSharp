// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Decodes packed TIFF color samples through a closed source and photometric operator.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
/// <typeparam name="TSource">The source sample layout.</typeparam>
/// <typeparam name="TOperator">The photometric conversion.</typeparam>
internal sealed class TiffColorDecoder<TPixel, TSource, TOperator> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
    where TSource : struct, ITiffColorSource<float>
    where TOperator : struct, ITiffColorOperator<float>
{
    private readonly Configuration configuration;
    private readonly TSource source;
    private readonly TOperator colorOperator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffColorDecoder{TPixel, TSource, TOperator}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="source">The stored sample reader.</param>
    /// <param name="colorOperator">The photometric conversion.</param>
    public TiffColorDecoder(Configuration configuration, TSource source, TOperator colorOperator)
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
        int offset = 0;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < width; x += vectors.Length)
            {
                int count = Math.Min(vectors.Length, width - x);
                Span<Vector4> block = vectors[..count];

                // The source defines byte stride; a complete pixel block is passed to the
                // register traversal, then PixelOperations applies the target's own format rules.
                TiffOperatedColorConverter<TSource, TOperator, TiffVector4Store, float, Vector4>.Convert(data.Slice(offset, count * this.source.BytesPerPixel), [], [], [], block, this.source, this.colorOperator, default);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, block, pixelRow.Slice(x, count), this.colorOperator.Modifiers);
                offset += count * this.source.BytesPerPixel;
            }
        }
    }
}
