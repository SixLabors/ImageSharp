// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
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
                // TIFF's three-component layout omits Vector4's fourth value.
                // The packer removes those words in full registers and handles
                // the final short block without changing floating-point bits.
                PackFloatTriplets(block, samples);
            }
            else
            {
                // A one-component source already stores intensity in component 0.
                // Other sources use the library's BT.709 luminance calculation.
                // The packer applies WhiteIsZero during the same SIMD traversal.
                PackFloatSingles(block, samples, this.singleComponentSource, this.whiteIsZero);
            }
        }
    }

    /// <summary>
    /// Packs the first three components of four-component vectors into consecutive
    /// three-component floating-point pixels without changing their bits.
    /// </summary>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The packed destination samples.</param>
    private static void PackFloatTriplets(ReadOnlySpan<Vector4> source, Span<float> destination)
    {
        ref byte s = ref Unsafe.As<Vector4, byte>(ref MemoryMarshal.GetReference(source));
        ref byte d = ref Unsafe.As<float, byte>(ref MemoryMarshal.GetReference(destination));
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<byte> firstThree = Vector128.Create(uint.MaxValue, uint.MaxValue, uint.MaxValue, 0U).AsByte();
            Vector128<byte> firstTwo = Vector128.Create(ulong.MaxValue, 0UL).AsByte();
            Vector128<byte> firstOne = Vector128.Create(uint.MaxValue, 0U, 0U, 0U).AsByte();

            for (; i <= source.Length - 4; i += 4)
            {
                nuint sourceByte = (nuint)(i * 16);
                Vector128<byte> pixel0 = Vector128.LoadUnsafe(ref s, sourceByte);
                Vector128<byte> pixel1 = Vector128.LoadUnsafe(ref s, sourceByte + 16);
                Vector128<byte> pixel2 = Vector128.LoadUnsafe(ref s, sourceByte + 32);
                Vector128<byte> pixel3 = Vector128.LoadUnsafe(ref s, sourceByte + 48);

                // Four input registers hold [c0,c1,c2,c3] for four pixels. The
                // masks discard each fourth component; byte shifts join retained
                // words across pixel boundaries into exactly three output registers.
                Vector128<byte> packed0 = (pixel0 & firstThree) | Vector128_.ShiftLeftBytesInVector(pixel1, 12);
                Vector128<byte> packed1 = (Vector128_.ShiftRightBytesInVector(pixel1, 4) & firstTwo) | Vector128_.ShiftLeftBytesInVector(pixel2, 8);
                Vector128<byte> packed2 = (Vector128_.ShiftRightBytesInVector(pixel2, 8) & firstOne) | Vector128_.ShiftLeftBytesInVector(pixel3, 4);

                nuint destinationByte = (nuint)(i * 12);
                Vector128.StoreUnsafe(packed0, ref d, destinationByte);
                Vector128.StoreUnsafe(packed1, ref d, destinationByte + 16);
                Vector128.StoreUnsafe(packed2, ref d, destinationByte + 32);
            }
        }

        for (; i < source.Length; i++)
        {
            Vector4 pixel = source[i];
            int sample = i * 3;
            destination[sample] = pixel.X;
            destination[sample + 1] = pixel.Y;
            destination[sample + 2] = pixel.Z;
        }
    }

    /// <summary>
    /// Packs four-component vectors into one floating-point sample per pixel.
    /// </summary>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination intensity samples.</param>
    /// <param name="useFirstComponent">Whether the source's first component is already its intensity.</param>
    /// <param name="invert">Whether to store one minus the intensity.</param>
    private static void PackFloatSingles(ReadOnlySpan<Vector4> source, Span<float> destination, bool useFirstComponent, bool invert)
    {
        ref float s = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(source));
        ref float d = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> one = Vector128.Create(1F);
            Vector4 weights = ColorNumerics.Bt709;
            Vector128<float> weight0 = Vector128.Create(weights.X);
            Vector128<float> weight1 = Vector128.Create(weights.Y);
            Vector128<float> weight2 = Vector128.Create(weights.Z);
            Vector128<float> weight3 = Vector128.Create(weights.W);

            for (; i <= source.Length - 4; i += 4)
            {
                Vector128<float> pixel0 = Vector128.LoadUnsafe(ref s, (nuint)(i * 4));
                Vector128<float> pixel1 = Vector128.LoadUnsafe(ref s, (nuint)((i + 1) * 4));
                Vector128<float> pixel2 = Vector128.LoadUnsafe(ref s, (nuint)((i + 2) * 4));
                Vector128<float> pixel3 = Vector128.LoadUnsafe(ref s, (nuint)((i + 3) * 4));

                // Transpose four pixels into component vectors. A grayscale source
                // uses the first component directly; a color source needs BT.709.
                Vector128<float> firstPair = Vector128_.UnpackLow(pixel0, pixel1);
                Vector128<float> secondPair = Vector128_.UnpackLow(pixel2, pixel3);
                Vector128<float> component0 = Vector128_.UnpackLow(firstPair.AsDouble(), secondPair.AsDouble()).AsSingle();
                Vector128<float> intensity;

                if (useFirstComponent)
                {
                    intensity = component0;
                }
                else
                {
                    Vector128<float> thirdPair = Vector128_.UnpackHigh(pixel0, pixel1);
                    Vector128<float> fourthPair = Vector128_.UnpackHigh(pixel2, pixel3);
                    Vector128<float> component1 = Vector128_.UnpackHigh(firstPair.AsDouble(), secondPair.AsDouble()).AsSingle();
                    Vector128<float> component2 = Vector128_.UnpackLow(thirdPair.AsDouble(), fourthPair.AsDouble()).AsSingle();
                    Vector128<float> component3 = Vector128_.UnpackHigh(thirdPair.AsDouble(), fourthPair.AsDouble()).AsSingle();

                    // Include the zero-weight fourth component. As with Vector4.Dot,
                    // a nonfinite fourth component then yields nonfinite luminance.
                    intensity = (((component0 * weight0) + (component1 * weight1))
                        + (component2 * weight2)) + (component3 * weight3);
                }

                if (invert)
                {
                    intensity = one - intensity;
                }

                Vector128.StoreUnsafe(intensity, ref d, (nuint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            float intensity = useFirstComponent ? source[i].X : ColorNumerics.GetBT709Luminance(source[i]);
            destination[i] = invert ? 1F - intensity : intensity;
        }
    }
}
