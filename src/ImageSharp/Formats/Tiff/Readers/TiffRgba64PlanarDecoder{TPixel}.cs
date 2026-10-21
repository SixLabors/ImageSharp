// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Decodes 16-bit RGBA planes while preserving their integer channel values.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
internal sealed class TiffRgba64PlanarDecoder<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool reverseEndianness;
    private readonly bool associated;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgba64PlanarDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="isBigEndian">Whether stored words use big-endian byte order.</param>
    /// <param name="associated">Whether stored RGB components have already been multiplied by alpha.</param>
    public TiffRgba64PlanarDecoder(Configuration configuration, bool isBigEndian, bool associated)
    {
        this.configuration = configuration;
        this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;
        this.associated = associated;
    }

    /// <inheritdoc />
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        // Each plane supplies one 16-bit value per pixel. Thirty-two pixels fill one 512-bit register per plane.
        InlineArray32<Rgba64> sampleBuffer = default;
        InlineArray32<Vector4> vectorBuffer = default;
        Span<Rgba64> samples = sampleBuffer;
        Span<Vector4> vectors = this.GetColorBuffer(this.configuration, width, vectorBuffer);
        ReadOnlySpan<byte> red = data[0].GetSpan();
        ReadOnlySpan<byte> green = data[1].GetSpan();
        ReadOnlySpan<byte> blue = data[2].GetSpan();
        ReadOnlySpan<byte> alpha = data[3].GetSpan();
        TiffPlanar16NativeSource source = new(this.reverseEndianness, 4);
        TiffRgba64Store store = default;
        TiffRgba64PlanarOperator colorOperator = default;
        int rowOffset = 0;
        PixelAlphaRepresentation alphaRepresentation = this.associated ? PixelAlphaRepresentation.Associated : PixelAlphaRepresentation.Unassociated;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < width; x += samples.Length)
            {
                int count = Math.Min(samples.Length, width - x);
                int byteOffset = rowOffset + (x * sizeof(ushort));
                int byteCount = count * sizeof(ushort);
                Span<Rgba64> sampleBlock = samples[..count];
                Span<ushort> nativeBlock = MemoryMarshal.Cast<Rgba64, ushort>(sampleBlock);

                if (this.associated)
                {
                    TiffOperatedColorConverter<TiffPlanar16NativeSource, TiffRgba64PlanarOperator, TiffRgba64Store, ushort, ushort>.Convert(
                        red.Slice(byteOffset, byteCount),
                        green.Slice(byteOffset, byteCount),
                        blue.Slice(byteOffset, byteCount),
                        alpha.Slice(byteOffset, byteCount),
                        nativeBlock,
                        source,
                        colorOperator,
                        store);

                    Span<Vector4> vectorBlock = this.GetColorBlock(vectors, x, count);
                    PixelOperations<Rgba64>.Instance.ToUnassociatedScaledVector4(this.configuration, sampleBlock, vectorBlock);

                    this.StoreColorVectors(this.configuration, vectorBlock, pixelRow.Slice(x, count), alphaRepresentation);
                }
                else
                {
                    // Pass exact 16-bit channel values to FromRgba64 so each destination format applies its integer conversion.
                    TiffNativeColorConverter<TiffPlanar16NativeSource, TiffRgba64Store, ushort>.Convert(
                        red.Slice(byteOffset, byteCount),
                        green.Slice(byteOffset, byteCount),
                        blue.Slice(byteOffset, byteCount),
                        alpha.Slice(byteOffset, byteCount),
                        nativeBlock,
                        source,
                        store);

                    if (!this.TryStoreColorPixels(this.configuration, (ReadOnlySpan<Rgba64>)sampleBlock, vectors, x))
                    {
                        PixelOperations<TPixel>.Instance.FromRgba64(this.configuration, sampleBlock, pixelRow.Slice(x, count));
                    }
                }
            }

            this.CompleteColorRow(this.configuration, vectors, pixelRow, alphaRepresentation);

            rowOffset += width * sizeof(ushort);
        }
    }
}
