// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

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
                int i = 0;

                if (Vector128.IsHardwareAccelerated)
                {
                    // A Vector4 pixel has four floats; four 128-bit float registers cover four complete pixels.
                    // Divide the block's float-register count by four, leaving incomplete groups to BitReader.
                    ReadOnlySpan<float> blockComponents = MemoryMarshal.Cast<Vector4, float>(block);
                    nuint groups = blockComponents.Vector128Count<float>() / 4;
                    for (nuint group = 0; group < groups; group++, i += Vector128<float>.Count)
                    {
                        this.source.ReadFour(row, x + i, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3);
                        this.colorOperator.Convert(ref c0, ref c1, ref c2, ref c3);

                        // Four component registers become four contiguous RGBA pixels without lane-by-lane stores.
                        SimdUtils.Transpose4(
                            c0,
                            c1,
                            c2,
                            c3,
                            out Vector128<float> pixel0,
                            out Vector128<float> pixel1,
                            out Vector128<float> pixel2,
                            out Vector128<float> pixel3);

                        ref Vector128<float> first = ref Unsafe.As<Vector4, Vector128<float>>(ref block[i]);
                        first = pixel0;
                        Unsafe.Add(ref first, 1) = pixel1;
                        Unsafe.Add(ref first, 2) = pixel2;
                        Unsafe.Add(ref first, 3) = pixel3;
                    }
                }

                // Four RGB444 pixels occupy six bytes and four RGBA4444 pixels occupy eight.
                // Both end on a byte boundary, so BitReader starts exactly at the remaining scalar tail.
                if (i < count)
                {
                    BitReader bitReader = new(row.Slice(((x + i) * this.source.BitsPerPixel) / 8));

                    for (; i < count; i++)
                    {
                        float c0 = bitReader.ReadBits(4);
                        float c1 = bitReader.ReadBits(4);
                        float c2 = bitReader.ReadBits(4);
                        float c3 = this.source.ComponentCount == 4 ? bitReader.ReadBits(4) : 0F;
                        this.colorOperator.Convert(ref c0, ref c1, ref c2, ref c3);
                        block[i] = new Vector4(c0, c1, c2, c3);
                    }
                }

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
