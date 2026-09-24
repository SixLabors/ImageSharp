// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Writes chunky IEEE binary32 grayscale, RGB, or RGBA TIFF samples.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffFloatWriter<TPixel> : TiffCompositeColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly int bitsPerPixel;
    private readonly bool whiteIsZero;
    private readonly bool clearTransparentPixels;
    private readonly bool singleComponentSource;
    private readonly PixelConversionModifiers modifiers;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffFloatWriter{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="entriesCollector">The TIFF directory collector.</param>
    /// <param name="bitsPerPixel">The total bits per pixel.</param>
    /// <param name="whiteIsZero">Whether a grayscale value of one represents black.</param>
    /// <param name="clearTransparentPixels">Whether zero-alpha color is cleared.</param>
    public TiffFloatWriter(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        int bitsPerPixel,
        bool whiteIsZero,
        bool clearTransparentPixels)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector)
    {
        this.bitsPerPixel = bitsPerPixel;
        this.whiteIsZero = whiteIsZero;
        this.clearTransparentPixels = clearTransparentPixels;

        PixelTypeInfo info = TPixel.GetPixelTypeInfo();
        this.singleComponentSource = info.ComponentInfo?.ComponentCount == 1;
        this.modifiers = bitsPerPixel == 128 && info.AlphaRepresentation == PixelAlphaRepresentation.Associated
            ? PixelConversionModifiers.Premultiply
            : PixelConversionModifiers.UnPremultiply;

        this.modifiers |= PixelConversionModifiers.Scale;
    }

    /// <inheritdoc />
    public override int BitsPerPixel => this.bitsPerPixel;

    /// <inheritdoc />
    protected override void EncodePixels(Span<TPixel> pixels, Span<byte> buffer)
    {
        const int BlockSize = 64;
        int samplesPerPixel = this.bitsPerPixel / 32;
        Span<float> output = MemoryMarshal.Cast<byte, float>(buffer);
        Span<Vector4> vectors = stackalloc Vector4[BlockSize];

        for (int offset = 0; offset < pixels.Length; offset += BlockSize)
        {
            int count = Math.Min(BlockSize, pixels.Length - offset);
            Span<Vector4> block = vectors[..count];
            PixelOperations<TPixel>.Instance.ToVector4(this.Configuration, pixels.Slice(offset, count), block, this.modifiers);

            // Clear in the native row buffer, after conversion, so clearing transparent color
            // never normalizes unrelated HDR pixels in the source image.
            if (samplesPerPixel == 4 && this.clearTransparentPixels)
            {
                EncodingUtilities.ReplaceTransparentPixels(block);
            }

            Span<float> samples = output.Slice(offset * samplesPerPixel, count * samplesPerPixel);
            if (samplesPerPixel == 4)
            {
                // The four-component vector layout matches chunky TIFF and DXGI float storage.
                MemoryMarshal.Cast<Vector4, float>(block).CopyTo(samples);
            }
            else if (samplesPerPixel == 3)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector4 pixel = block[i];
                    int sample = i * 3;
                    samples[sample] = pixel.X;
                    samples[sample + 1] = pixel.Y;
                    samples[sample + 2] = pixel.Z;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    // A one-component float pixel stores its intensity in component 0. Applying
                    // color weights would change that value because its other components are zero.
                    float intensity = this.singleComponentSource
                        ? block[i].X
                        : ColorNumerics.GetBT709Luminance(block[i]);

                    samples[i] = this.whiteIsZero ? 1F - intensity : intensity;
                }
            }
        }
    }
}
