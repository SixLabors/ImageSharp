// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp.ColorConverters;

internal abstract partial class WebpColorConverterBase
{
    private sealed partial class WebpColorConverter<TOperator>
    {
        /// <inheritdoc />
        public override bool ConvertFromRgb<TPixel>(
            Buffer2DRegion<TPixel> frame,
            Configuration configuration,
            MemoryAllocator memoryAllocator,
            ColorProfileConverter? converter,
            Span<Vector4> vectors,
            Span<Rgb> rgb,
            Span<byte> y,
            Span<byte> u,
            Span<byte> v)
        {
            int width = frame.Width;
            int height = frame.Height;
            int uvWidth = (width + 1) >> 1;

            // Reuse two BGRA source rows and one resampled RGB row. Chroma needs both source rows in linear light
            // before the color matrix runs; luma still uses each original full-resolution row.
            using IMemoryOwner<ushort> resampledRgbBuffer = memoryAllocator.Allocate<ushort>(4 * uvWidth);
            using IMemoryOwner<Bgra32> bgraRow0Buffer = memoryAllocator.Allocate<Bgra32>(width);
            using IMemoryOwner<Bgra32> bgraRow1Buffer = memoryAllocator.Allocate<Bgra32>(width);
            Span<ushort> resampledRgb = resampledRgbBuffer.GetSpan();
            Span<Bgra32> bgraRow0 = bgraRow0Buffer.GetSpan();
            Span<Bgra32> bgraRow1 = bgraRow1Buffer.GetSpan();
            int uvRowIndex = 0;
            int rowIndex;
            bool hasAlpha = false;

            for (rowIndex = 0; rowIndex < height - 1; rowIndex += 2)
            {
                Span<TPixel> rowSpan = frame.DangerousGetRowSpan(rowIndex);
                Span<TPixel> nextRowSpan = frame.DangerousGetRowSpan(rowIndex + 1);
                if (converter is null)
                {
                    PixelOperations<TPixel>.Instance.ToBgra32(configuration, rowSpan, bgraRow0);
                    PixelOperations<TPixel>.Instance.ToBgra32(configuration, nextRowSpan, bgraRow1);
                }
                else
                {
                    // Apply ICC before the existing luma matrix and alpha-weighted chroma resampling.
                    // The same float scratch is reused after each BGRA row has been packed.
                    this.ConvertRgbRow<TPixel, Bgra32>(configuration, rowSpan, converter, vectors, rgb, bgraRow0);
                    this.ConvertRgbRow<TPixel, Bgra32>(configuration, nextRowSpan, converter, vectors, rgb, bgraRow1);
                }

                // Both rows contribute to each chroma sample; transparency in either row requires alpha-weighted resampling.
                bool rowsHaveAlpha = WebpCommonUtils.CheckNonOpaque(bgraRow0) || WebpCommonUtils.CheckNonOpaque(bgraRow1);
                if (rowsHaveAlpha)
                {
                    hasAlpha = true;
                }

                // Downsample U/V planes, two rows at a time.
                if (!rowsHaveAlpha)
                {
                    AccumulateRgb(bgraRow0, bgraRow1, resampledRgb, width);
                }
                else
                {
                    AccumulateRgba(bgraRow0, bgraRow1, resampledRgb, width);
                }

                ConvertChromaRow(resampledRgb, u[(uvRowIndex * uvWidth)..], v[(uvRowIndex * uvWidth)..]);
                uvRowIndex++;

                ConvertLumaRow(bgraRow0, y[(rowIndex * width)..]);
                ConvertLumaRow(bgraRow1, y[((rowIndex + 1) * width)..]);
            }

            // Repeat the final row when height is odd so chroma still receives a complete 2x2 sample group.
            if ((height & 1) != 0)
            {
                Span<TPixel> rowSpan = frame.DangerousGetRowSpan(rowIndex);
                if (converter is null)
                {
                    PixelOperations<TPixel>.Instance.ToBgra32(configuration, rowSpan, bgraRow0);
                }
                else
                {
                    // For an odd height, convert the final row into the output profile before YCbCr encoding.
                    this.ConvertRgbRow<TPixel, Bgra32>(configuration, rowSpan, converter, vectors, rgb, bgraRow0);
                }

                ConvertLumaRow(bgraRow0, y[(rowIndex * width)..]);

                if (!WebpCommonUtils.CheckNonOpaque(bgraRow0))
                {
                    AccumulateRgb(bgraRow0, bgraRow0, resampledRgb, width);
                }
                else
                {
                    AccumulateRgba(bgraRow0, bgraRow0, resampledRgb, width);
                    hasAlpha = true;
                }

                ConvertChromaRow(resampledRgb, u[(uvRowIndex * uvWidth)..], v[(uvRowIndex * uvWidth)..]);
            }

            return hasAlpha;
        }

        /// <summary>
        /// Converts BGRA pixels to full-resolution luma.
        /// </summary>
        /// <param name="pixels">The source pixels in B,G,R,A byte order.</param>
        /// <param name="y">The output luma samples.</param>
        private static void ConvertLumaRow(ReadOnlySpan<Bgra32> pixels, Span<byte> y)
        {
            // Reading each BGRA pixel as a uint exposes B in bits 0..7, G in 8..15, and R in 16..23.
            ReadOnlySpan<uint> samples = MemoryMarshal.Cast<Bgra32, uint>(pixels);
            ref uint source = ref MemoryMarshal.GetReference(samples);
            ref byte destination = ref MemoryMarshal.GetReference(y);
            int sampleOffset = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                // 16 int lanes convert 16 samples. Complete groups bound all source loads and the narrowed byte stores.
                int sampleEnd = sampleOffset + ((int)samples[sampleOffset..].Vector512Count<uint>() * Vector512<int>.Count);

                for (; sampleOffset < sampleEnd; sampleOffset += Vector512<int>.Count)
                {
                    Vector512<uint> packed = Vector512.LoadUnsafe(ref source, (nuint)sampleOffset);
                    Vector512<uint> mask = Vector512.Create((uint)byte.MaxValue);
                    Vector512<int> red = ((packed >> 16) & mask).AsInt32();
                    Vector512<int> green = ((packed >> 8) & mask).AsInt32();
                    Vector512<int> blue = (packed & mask).AsInt32();

                    // Pixel codes have no fractional bits. Static binding and inlining let the JIT discard unused chroma work.
                    TOperator.ConvertFromRgb(red, green, blue, 0, out Vector512<int> luma, out _, out _);
                    StoreConvertedSamples(luma, ref Unsafe.Add(ref destination, (uint)sampleOffset));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                // Eight int lanes convert eight samples. Complete groups bound all source loads and the narrowed byte stores.
                int sampleEnd = sampleOffset + ((int)samples[sampleOffset..].Vector256Count<uint>() * Vector256<int>.Count);

                for (; sampleOffset < sampleEnd; sampleOffset += Vector256<int>.Count)
                {
                    Vector256<uint> packed = Vector256.LoadUnsafe(ref source, (nuint)sampleOffset);
                    Vector256<uint> mask = Vector256.Create((uint)byte.MaxValue);
                    Vector256<int> red = ((packed >> 16) & mask).AsInt32();
                    Vector256<int> green = ((packed >> 8) & mask).AsInt32();
                    Vector256<int> blue = (packed & mask).AsInt32();

                    // Pixel codes have no fractional bits. Static binding and inlining let the JIT discard unused chroma work.
                    TOperator.ConvertFromRgb(red, green, blue, 0, out Vector256<int> luma, out _, out _);
                    StoreConvertedSamples(luma, ref Unsafe.Add(ref destination, (uint)sampleOffset));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // Four int lanes convert four samples. Complete groups bound all source loads and the narrowed byte stores.
                int sampleEnd = sampleOffset + ((int)samples[sampleOffset..].Vector128Count<uint>() * Vector128<int>.Count);

                for (; sampleOffset < sampleEnd; sampleOffset += Vector128<int>.Count)
                {
                    Vector128<uint> packed = Vector128.LoadUnsafe(ref source, (nuint)sampleOffset);
                    Vector128<uint> mask = Vector128.Create((uint)byte.MaxValue);
                    Vector128<int> red = ((packed >> 16) & mask).AsInt32();
                    Vector128<int> green = ((packed >> 8) & mask).AsInt32();
                    Vector128<int> blue = (packed & mask).AsInt32();

                    // Pixel codes have no fractional bits. Static binding and inlining let the JIT discard unused chroma work.
                    TOperator.ConvertFromRgb(red, green, blue, 0, out Vector128<int> luma, out _, out _);
                    StoreConvertedSamples(luma, ref Unsafe.Add(ref destination, (uint)sampleOffset));
                }
            }

            // Finish a partial vector, or the entire row when hardware acceleration is unavailable.
            for (; sampleOffset < pixels.Length; sampleOffset++)
            {
                Bgra32 pixel = pixels[sampleOffset];
                TOperator.ConvertFromRgb(pixel.R, pixel.G, pixel.B, 0, out int luma, out _, out _);
                y[sampleOffset] = (byte)luma;
            }
        }

        /// <summary>
        /// Converts resampled RGB values with two fractional bits into half-resolution U and V rows.
        /// </summary>
        /// <param name="rgb">Four words per sample: red, green, blue, and an unused fourth word.</param>
        /// <param name="u">The output U samples.</param>
        /// <param name="v">The output V samples.</param>
        private static void ConvertChromaRow(ReadOnlySpan<ushort> rgb, Span<byte> u, Span<byte> v)
        {
            // Each ulong contains one R,G,B,unused group. The fourth word need not be initialized and is never selected.
            ReadOnlySpan<ulong> samples = MemoryMarshal.Cast<ushort, ulong>(rgb);
            ref ulong source = ref MemoryMarshal.GetReference(samples);
            ref byte uDestination = ref MemoryMarshal.GetReference(u);
            ref byte vDestination = ref MemoryMarshal.GetReference(v);
            int sampleOffset = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                // 16 int lanes convert 16 samples. Complete groups bound all source loads and the narrowed byte stores.
                int sampleEnd = sampleOffset + ((int)(samples[sampleOffset..].Vector512Count<ulong>() / 2) * Vector512<int>.Count);

                for (; sampleOffset < sampleEnd; sampleOffset += Vector512<int>.Count)
                {
                    // Two ulong vectors contain enough packed samples for one int vector per RGB channel.
                    // Shift each channel into the low word, then narrow in sample order and discard the other words.
                    Vector512<ulong> first = Vector512.LoadUnsafe(ref source, (nuint)sampleOffset);

                    Vector512<ulong> second = Vector512.LoadUnsafe(ref source, (nuint)(sampleOffset + Vector512<ulong>.Count));
                    Vector512<uint> mask = Vector512.Create(0xFFFFU);
                    Vector512<int> red = (Vector512.Narrow(first, second) & mask).AsInt32();
                    Vector512<int> green = (Vector512.Narrow(first >> 16, second >> 16) & mask).AsInt32();
                    Vector512<int> blue = (Vector512.Narrow(first >> 32, second >> 32) & mask).AsInt32();

                    // Resampling retains two fractional bits. Static binding and inlining let the JIT discard unused luma work.
                    TOperator.ConvertFromRgb(red, green, blue, 2, out _, out Vector512<int> uValues, out Vector512<int> vValues);
                    StoreConvertedSamples(uValues, ref Unsafe.Add(ref uDestination, (uint)sampleOffset));
                    StoreConvertedSamples(vValues, ref Unsafe.Add(ref vDestination, (uint)sampleOffset));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                // Eight int lanes convert eight samples. Complete groups bound all source loads and the narrowed byte stores.
                int sampleEnd = sampleOffset + ((int)(samples[sampleOffset..].Vector256Count<ulong>() / 2) * Vector256<int>.Count);

                for (; sampleOffset < sampleEnd; sampleOffset += Vector256<int>.Count)
                {
                    // Two ulong vectors contain enough packed samples for one int vector per RGB channel.
                    // Shift each channel into the low word, then narrow in sample order and discard the other words.
                    Vector256<ulong> first = Vector256.LoadUnsafe(ref source, (nuint)sampleOffset);

                    Vector256<ulong> second = Vector256.LoadUnsafe(ref source, (nuint)(sampleOffset + Vector256<ulong>.Count));
                    Vector256<uint> mask = Vector256.Create(0xFFFFU);
                    Vector256<int> red = (Vector256.Narrow(first, second) & mask).AsInt32();
                    Vector256<int> green = (Vector256.Narrow(first >> 16, second >> 16) & mask).AsInt32();
                    Vector256<int> blue = (Vector256.Narrow(first >> 32, second >> 32) & mask).AsInt32();

                    // Resampling retains two fractional bits. Static binding and inlining let the JIT discard unused luma work.
                    TOperator.ConvertFromRgb(red, green, blue, 2, out _, out Vector256<int> uValues, out Vector256<int> vValues);
                    StoreConvertedSamples(uValues, ref Unsafe.Add(ref uDestination, (uint)sampleOffset));
                    StoreConvertedSamples(vValues, ref Unsafe.Add(ref vDestination, (uint)sampleOffset));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // Four int lanes convert four samples. Complete groups bound all source loads and the narrowed byte stores.
                int sampleEnd = sampleOffset + ((int)(samples[sampleOffset..].Vector128Count<ulong>() / 2) * Vector128<int>.Count);

                for (; sampleOffset < sampleEnd; sampleOffset += Vector128<int>.Count)
                {
                    // Two ulong vectors contain enough packed samples for one int vector per RGB channel.
                    // Shift each channel into the low word, then narrow in sample order and discard the other words.
                    Vector128<ulong> first = Vector128.LoadUnsafe(ref source, (nuint)sampleOffset);

                    Vector128<ulong> second = Vector128.LoadUnsafe(ref source, (nuint)(sampleOffset + Vector128<ulong>.Count));
                    Vector128<uint> mask = Vector128.Create(0xFFFFU);
                    Vector128<int> red = (Vector128.Narrow(first, second) & mask).AsInt32();
                    Vector128<int> green = (Vector128.Narrow(first >> 16, second >> 16) & mask).AsInt32();
                    Vector128<int> blue = (Vector128.Narrow(first >> 32, second >> 32) & mask).AsInt32();

                    // Resampling retains two fractional bits. Static binding and inlining let the JIT discard unused luma work.
                    TOperator.ConvertFromRgb(red, green, blue, 2, out _, out Vector128<int> uValues, out Vector128<int> vValues);
                    StoreConvertedSamples(uValues, ref Unsafe.Add(ref uDestination, (uint)sampleOffset));
                    StoreConvertedSamples(vValues, ref Unsafe.Add(ref vDestination, (uint)sampleOffset));
                }
            }

            // Remaining samples use the identical integer matrix, including the two fractional input bits.
            for (; sampleOffset < samples.Length; sampleOffset++)
            {
                ulong packed = samples[sampleOffset];
                int red = (int)(packed & 0xFFFF);
                int green = (int)((packed >> 16) & 0xFFFF);
                int blue = (int)((packed >> 32) & 0xFFFF);
                TOperator.ConvertFromRgb(red, green, blue, 2, out _, out int uValue, out int vValue);
                u[sampleOffset] = (byte)uValue;
                v[sampleOffset] = (byte)vValue;
            }
        }

        /// <summary>
        /// Stores sixteen byte-range channel codes in their original sample order.
        /// </summary>
        /// <param name="values">The converted channel values, each in the range 0..255.</param>
        /// <param name="destination">The first of sixteen output bytes.</param>
        private static void StoreConvertedSamples(Vector512<int> values, ref byte destination)
        {
            // Reuse the decoder's exact sixteen-sample narrowing; all values already fit in a byte.
            Narrow(values).StoreUnsafe(ref destination);
        }

        /// <summary>
        /// Stores eight byte-range channel codes in their original sample order.
        /// </summary>
        /// <param name="values">The converted channel values, each in the range 0..255.</param>
        /// <param name="destination">The first of eight output bytes.</param>
        private static void StoreConvertedSamples(Vector256<int> values, ref byte destination)
        {
            Vector128<ushort> words = Vector128.Narrow(values.GetLower().AsUInt32(), values.GetUpper().AsUInt32());
            Vector128<byte> bytes = Vector128.Narrow(words, Vector128<ushort>.Zero);

            // Only the low eight bytes contain samples; an eight-byte store cannot overwrite the following row data.
            Unsafe.WriteUnaligned(ref destination, bytes.AsUInt64().ToScalar());
        }

        /// <summary>
        /// Stores four byte-range channel codes in their original sample order.
        /// </summary>
        /// <param name="values">The converted channel values, each in the range 0..255.</param>
        /// <param name="destination">The first of four output bytes.</param>
        private static void StoreConvertedSamples(Vector128<int> values, ref byte destination)
        {
            Vector128<ushort> words = Vector128.Narrow(values.AsUInt32(), Vector128<uint>.Zero);
            Vector128<byte> bytes = Vector128.Narrow(words, Vector128<ushort>.Zero);

            // Narrowing places the four samples in the low word. Store exactly those four bytes.
            Unsafe.WriteUnaligned(ref destination, bytes.AsUInt32().ToScalar());
        }

        /// <summary>
        /// Resamples each 2x2 RGB group in linear light and retains two fractional bits for chroma conversion.
        /// </summary>
        /// <param name="rowSpan">The first source row.</param>
        /// <param name="nextRowSpan">The second source row, or the first row again at the bottom edge.</param>
        /// <param name="dst">Four words per output sample: red, green, blue, and an unused fourth word.</param>
        /// <param name="width">The number of source pixels per row.</param>
        private static void AccumulateRgb(ReadOnlySpan<Bgra32> rowSpan, ReadOnlySpan<Bgra32> nextRowSpan, Span<ushort> dst, int width)
        {
            Bgra32 topLeft;
            Bgra32 topRight;
            int sampleIndex, pixelOffset;
            int destinationOffset = 0;

            // A 2x2 group produces one chroma sample; gamma conversion happens before averaging the four source values.
            for (sampleIndex = 0, pixelOffset = 0; sampleIndex < (width >> 1); sampleIndex += 1, pixelOffset += 2, destinationOffset += 4)
            {
                topLeft = rowSpan[pixelOffset];
                topRight = rowSpan[pixelOffset + 1];
                Bgra32 bottomLeft = nextRowSpan[pixelOffset];
                Bgra32 bottomRight = nextRowSpan[pixelOffset + 1];

                dst[destinationOffset] = (ushort)LinearToGamma(
                    GammaToLinear(topLeft.R) + GammaToLinear(topRight.R) + GammaToLinear(bottomLeft.R) + GammaToLinear(bottomRight.R), 0);

                dst[destinationOffset + 1] = (ushort)LinearToGamma(
                    GammaToLinear(topLeft.G) + GammaToLinear(topRight.G) + GammaToLinear(bottomLeft.G) + GammaToLinear(bottomRight.G), 0);

                dst[destinationOffset + 2] = (ushort)LinearToGamma(
                    GammaToLinear(topLeft.B) + GammaToLinear(topRight.B) + GammaToLinear(bottomLeft.B) + GammaToLinear(bottomRight.B), 0);
            }

            // Repeat the rightmost source column when width is odd. The doubled contribution keeps the four-sample scale.
            if ((width & 1) != 0)
            {
                topLeft = rowSpan[pixelOffset];
                Bgra32 bottomLeft = nextRowSpan[pixelOffset];

                dst[destinationOffset] = (ushort)LinearToGamma(GammaToLinear(topLeft.R) + GammaToLinear(bottomLeft.R), 1);
                dst[destinationOffset + 1] = (ushort)LinearToGamma(GammaToLinear(topLeft.G) + GammaToLinear(bottomLeft.G), 1);
                dst[destinationOffset + 2] = (ushort)LinearToGamma(GammaToLinear(topLeft.B) + GammaToLinear(bottomLeft.B), 1);
            }
        }

        /// <summary>
        /// Resamples each 2x2 RGB group in linear light with alpha weighting and retains two fractional bits.
        /// </summary>
        /// <param name="rowSpan">The first source row.</param>
        /// <param name="nextRowSpan">The second source row, or the first row again at the bottom edge.</param>
        /// <param name="dst">Four words per output sample: red, green, blue, and the sum of the contributing alpha values.</param>
        /// <param name="width">The number of source pixels per row.</param>
        private static void AccumulateRgba(ReadOnlySpan<Bgra32> rowSpan, ReadOnlySpan<Bgra32> nextRowSpan, Span<ushort> dst, int width)
        {
            Bgra32 topLeft;
            Bgra32 topRight;
            int sampleIndex, pixelOffset;
            int destinationOffset = 0;

            // A 2x2 group produces one chroma sample; gamma conversion happens before averaging the four source values.
            for (sampleIndex = 0, pixelOffset = 0; sampleIndex < width >> 1; sampleIndex += 1, pixelOffset += 2, destinationOffset += 4)
            {
                topLeft = rowSpan[pixelOffset];
                topRight = rowSpan[pixelOffset + 1];
                Bgra32 bottomLeft = nextRowSpan[pixelOffset];
                Bgra32 bottomRight = nextRowSpan[pixelOffset + 1];
                uint alphaSum = (uint)(topLeft.A + topRight.A + bottomLeft.A + bottomRight.A);
                int r, g, b;

                // Fully opaque samples need no weighting. A fully transparent group has no visible color and avoids division by zero.
                if (alphaSum is 4 * 0xff or 0)
                {
                    r = (ushort)LinearToGamma(
                        GammaToLinear(topLeft.R) + GammaToLinear(topRight.R) + GammaToLinear(bottomLeft.R) + GammaToLinear(bottomRight.R), 0);

                    g = (ushort)LinearToGamma(
                        GammaToLinear(topLeft.G) + GammaToLinear(topRight.G) + GammaToLinear(bottomLeft.G) + GammaToLinear(bottomRight.G), 0);

                    b = (ushort)LinearToGamma(
                        GammaToLinear(topLeft.B) + GammaToLinear(topRight.B) + GammaToLinear(bottomLeft.B) + GammaToLinear(bottomRight.B), 0);
                }
                else
                {
                    r = LinearToGammaWeighted(topLeft.R, topRight.R, bottomLeft.R, bottomRight.R, topLeft.A, topRight.A, bottomLeft.A, bottomRight.A, alphaSum);
                    g = LinearToGammaWeighted(topLeft.G, topRight.G, bottomLeft.G, bottomRight.G, topLeft.A, topRight.A, bottomLeft.A, bottomRight.A, alphaSum);
                    b = LinearToGammaWeighted(topLeft.B, topRight.B, bottomLeft.B, bottomRight.B, topLeft.A, topRight.A, bottomLeft.A, bottomRight.A, alphaSum);
                }

                dst[destinationOffset] = (ushort)r;
                dst[destinationOffset + 1] = (ushort)g;
                dst[destinationOffset + 2] = (ushort)b;
                dst[destinationOffset + 3] = (ushort)alphaSum;
            }

            // Repeat the rightmost source column when width is odd. The doubled contribution keeps the four-sample scale.
            if ((width & 1) != 0)
            {
                topLeft = rowSpan[pixelOffset];
                Bgra32 bottomLeft = nextRowSpan[pixelOffset];
                uint alphaSum = (uint)(2u * (topLeft.A + bottomLeft.A));
                int r, g, b;

                // Fully opaque samples need no weighting. A fully transparent group has no visible color and avoids division by zero.
                if (alphaSum is 4 * 0xff or 0)
                {
                    r = (ushort)LinearToGamma(GammaToLinear(topLeft.R) + GammaToLinear(bottomLeft.R), 1);
                    g = (ushort)LinearToGamma(GammaToLinear(topLeft.G) + GammaToLinear(bottomLeft.G), 1);
                    b = (ushort)LinearToGamma(GammaToLinear(topLeft.B) + GammaToLinear(bottomLeft.B), 1);
                }
                else
                {
                    r = LinearToGammaWeighted(topLeft.R, bottomLeft.R, topLeft.R, bottomLeft.R, topLeft.A, bottomLeft.A, topLeft.A, bottomLeft.A, alphaSum);
                    g = LinearToGammaWeighted(topLeft.G, bottomLeft.G, topLeft.G, bottomLeft.G, topLeft.A, bottomLeft.A, topLeft.A, bottomLeft.A, alphaSum);
                    b = LinearToGammaWeighted(topLeft.B, bottomLeft.B, topLeft.B, bottomLeft.B, topLeft.A, bottomLeft.A, topLeft.A, bottomLeft.A, alphaSum);
                }

                dst[destinationOffset] = (ushort)r;
                dst[destinationOffset + 1] = (ushort)g;
                dst[destinationOffset + 2] = (ushort)b;
                dst[destinationOffset + 3] = (ushort)alphaSum;
            }
        }

        /// <summary>
        /// Averages four linear-light values by alpha and returns a gamma-encoded value with two fractional bits.
        /// </summary>
        /// <param name="rgb0">The first channel value.</param>
        /// <param name="rgb1">The second channel value.</param>
        /// <param name="rgb2">The third channel value.</param>
        /// <param name="rgb3">The fourth channel value.</param>
        /// <param name="a0">The first alpha value.</param>
        /// <param name="a1">The second alpha value.</param>
        /// <param name="a2">The third alpha value.</param>
        /// <param name="a3">The fourth alpha value.</param>
        /// <param name="totalA">The nonzero sum of the four alpha values.</param>
        /// <returns>The weighted channel value scaled by four.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int LinearToGammaWeighted(byte rgb0, byte rgb1, byte rgb2, byte rgb3, byte a0, byte a1, byte a2, byte a3, uint totalA)
        {
            uint sum = (a0 * GammaToLinear(rgb0)) + (a1 * GammaToLinear(rgb1)) + (a2 * GammaToLinear(rgb2)) + (a3 * GammaToLinear(rgb3));

            // InvAlpha supplies the fixed-point reciprocal of the alpha sum; retain two extra bits for the chroma matrix.
            return LinearToGamma((sum * WebpLookupTables.InvAlpha[totalA]) >> (WebpConstants.AlphaFix - 2), 0);
        }

        /// <summary>
        /// Converts a linear-light sum to a gamma-encoded channel value with two fractional bits.
        /// </summary>
        /// <param name="baseValue">The sum of the linear-light source values.</param>
        /// <param name="shift">One when two samples replace the four-sample group; otherwise zero.</param>
        /// <returns>The gamma-encoded channel value scaled by four.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int LinearToGamma(uint baseValue, int shift)
        {
            // At an odd image edge, doubling a two-sample sum keeps the same scale as a complete 2x2 group.
            int interpolated = Interpolate((int)(baseValue << shift));
            return (interpolated + WebpConstants.GammaTabRounder) >> WebpConstants.GammaTabFix;
        }

        /// <summary>
        /// Maps a gamma-encoded byte to the linear-light lookup-table scale.
        /// </summary>
        /// <param name="v">The gamma-encoded channel byte.</param>
        /// <returns>The linear-light value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint GammaToLinear(byte v) => WebpLookupTables.GammaToLinearTab[v];

        /// <summary>
        /// Interpolates neighboring entries in the linear-to-gamma table for a four-sample sum.
        /// </summary>
        /// <param name="v">The linear-light sum at the four-sample scale.</param>
        /// <returns>The interpolated gamma value at the table's fixed-point scale.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Interpolate(int v)
        {
            // The low bits give the position between table entries. The extra two bits account for the four-sample sum.
            int tabPos = v >> (WebpConstants.GammaTabFix + 2);
            int x = v & ((WebpConstants.GammaTabScale << 2) - 1);
            int v0 = WebpLookupTables.LinearToGammaTab[tabPos];
            int v1 = WebpLookupTables.LinearToGammaTab[tabPos + 1];
            int y = (v1 * x) + (v0 * ((WebpConstants.GammaTabScale << 2) - x));

            return y;
        }
    }
}
